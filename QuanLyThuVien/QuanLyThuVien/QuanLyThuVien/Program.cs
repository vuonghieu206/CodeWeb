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

// ---------- MVC ----------
// Mặc định mọi trang đều cần đăng nhập; trang công khai ghi [AllowAnonymous].
// Các trang quản trị kế thừa StaffController (chỉ Admin/Thủ thư), trang độc giả dùng [Authorize(Roles = "Độc giả")].
builder.Services
    .AddControllersWithViews(options => options.Filters.Add(new AuthorizeFilter()));

// ---------- Định dạng ngày/số theo tiếng Việt (vi-VN) ----------
var viCulture = new CultureInfo("vi-VN");

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
    o.AddFixedWindowLimiter("login", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
    o.AddFixedWindowLimiter("register", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
    o.AddFixedWindowLimiter("lookup", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
    o.AddFixedWindowLimiter("checkout", c => { c.PermitLimit = 10; c.Window = TimeSpan.FromMinutes(1); c.QueueLimit = 0; });
});

// ---------- Dịch vụ riêng ----------
builder.Services.AddMemoryCache();   // đếm số lần đăng nhập sai
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
    var accountService = scope.ServiceProvider.GetRequiredService<AccountService>();

    if (!db.Accounts.Any())
    {
        // Tài khoản quản trị mẫu
        var admin = new Account { Username = "admin", Role = AppRoles.Admin, IsActive = true };
        admin.Password = accountService.Hash(admin, "Admin@123");
        db.Accounts.Add(admin);

        // Tài khoản độc giả mẫu để thử giao diện Khách
        var demo = new Account { Username = "docgia", Role = AppRoles.DocGia, IsActive = true };
        demo.Password = accountService.Hash(demo, "Docgia123");
        db.Accounts.Add(demo);
        db.SaveChanges();

        db.Readers.Add(new Reader
        {
            FullName = "Độc giả Demo", PhoneNumber = "0912345678",
            Email = "docgia@example.com", Address = "Hà Nội", AccountId = demo.Id
        });
        db.SaveChanges();
    }

    // Danh mục: lấy từ các thể loại sách đã có; nếu chưa có gì thì tạo vài danh mục mẫu
    if (!db.Categories.Any())
    {
        var names = db.Books.Where(b => b.Category != null && b.Category != "")
            .Select(b => b.Category!).ToList()
            .Select(n => n.Trim()).Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            names = new List<string> { "Thiếu nhi", "Tiểu thuyết", "Kỹ năng", "Công nghệ", "Khoa học", "Lịch sử" };
        db.Categories.AddRange(names.Select(n => new Category { Name = n }));
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
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(viCulture),
    SupportedCultures = new List<CultureInfo> { viCulture },
    SupportedUICultures = new List<CultureInfo> { viCulture }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();

// "/" (F5) -> giao diện Khách
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=KhachHang}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
