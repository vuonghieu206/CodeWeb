using System.Globalization;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- MVC + mặc định "cần đăng nhập" cho toàn bộ site ----------
// Controller nào công khai (Khách, Đăng nhập, API đọc sách) phải ghi [AllowAnonymous] -> an toàn theo mặc định.
var staffPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireRole(AppRoles.Admin, AppRoles.ThuThu)
    .Build();

builder.Services
    .AddControllersWithViews(options => options.Filters.Add(new AuthorizeFilter(staffPolicy)))
    .AddViewLocalization()    // đa ngôn ngữ trong View
    .AddDataAnnotationsLocalization(o =>   // dịch cả thông báo lỗi validation
        o.DataAnnotationLocalizerProvider = (type, factory) => factory.Create(typeof(QuanLyThuVien.SharedResource)));

// ---------- Quốc tế hóa (i18n): vi (mặc định) + en ----------
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
var supportedCultures = new[] { new CultureInfo("vi"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    o.DefaultRequestCulture = new RequestCulture("vi");
    o.SupportedCultures = supportedCultures;
    o.SupportedUICultures = supportedCultures;
    // Ưu tiên cookie ".AspNetCore.Culture" do nút chọn ngôn ngữ ghi ra
    o.RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() };
});

// ---------- Database ----------
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Session (giỏ mượn của Khách) ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.Cookie.Name = ".QLTV.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// ---------- Đăng nhập bằng Cookie ----------
var securePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Auth/Login";
        o.AccessDeniedPath = "/Auth/AccessDenied";
        o.Cookie.Name = ".QLTV.Auth";
        o.Cookie.HttpOnly = true;                 // JS không đọc được cookie -> chống XSS đánh cắp phiên
        o.Cookie.SameSite = SameSiteMode.Lax;     // giảm CSRF
        o.Cookie.SecurePolicy = securePolicy;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // API trả 401/403 thay vì chuyển hướng sang trang đăng nhập
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// ---------- Chống CSRF (Anti-forgery) cho AJAX: gửi token qua header ----------
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "RequestVerificationToken";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = securePolicy;
});

// ---------- Giới hạn tần suất (chống dò mật khẩu / spam) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("lookup", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
    o.AddFixedWindowLimiter("checkout", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
});

// ---------- Dịch vụ riêng ----------
builder.Services.AddSingleton<AccountService>();
builder.Services.AddScoped<LateFeeService>();
builder.Services.AddHostedService<OverdueFeeWorker>();   // tự ghi phạt phiếu quá hạn
builder.Services.AddScoped<ImageUploadService>();
// Làm sạch HTML từ editor (chặn <script>, onerror=..., v.v.) -> chống XSS
builder.Services.AddSingleton<IHtmlSanitizer>(_ =>
{
    var s = new HtmlSanitizer();
    s.AllowedTags.Clear();
    foreach (var t in new[] { "p", "br", "b", "strong", "i", "em", "u", "s", "ul", "ol", "li", "blockquote", "h2", "h3", "h4", "a", "span" })
        s.AllowedTags.Add(t);
    s.AllowedAttributes.Clear();
    s.AllowedAttributes.Add("href");
    s.AllowedSchemes.Clear();
    s.AllowedSchemes.Add("http");
    s.AllowedSchemes.Add("https");
    return s;
});

var app = builder.Build();

// ---------- Tự tạo/cập nhật DB và dữ liệu mẫu ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    db.Database.Migrate();
    if (!db.Accounts.Any())
    {
        var acc = new Account { Username = "admin", Role = AppRoles.Admin, IsActive = true };
        acc.Password = scope.ServiceProvider.GetRequiredService<AccountService>().Hash(acc, "Admin@123");
        db.Accounts.Add(acc);
        db.SaveChanges();
    }
}

// ---------- Pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Header bảo mật cơ bản
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStaticFiles();   // phục vụ ảnh upload lúc chạy (MapStaticAssets chỉ biết file lúc build)
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();

// "/" (F5) -> trang đăng nhập
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Auth", action = "Login" });

// Các địa chỉ còn lại: action mặc định là Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=KhachHang}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
