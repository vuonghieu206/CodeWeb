using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    // Đăng ký tài khoản độc giả: thông tin cá nhân sẽ tự tạo thành hồ sơ độc giả
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Chỉ dùng chữ không dấu, số, dấu . và _")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu")]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AuthController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly AccountService _accounts;

        public AuthController(LibraryDbContext context, AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        private static bool IsStaff(string role) => role == AppRoles.Admin || role == AppRoles.ThuThu;

        // Trang đăng nhập riêng (dùng khi truy cập thẳng /Auth/Login hoặc bị chuyển hướng vì chưa đăng nhập)
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return User.IsInRole(AppRoles.DocGia) ? RedirectToAction("Index", "KhachHang") : RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // Dùng chung cho Admin / Thủ thư / Độc giả. Gọi bằng AJAX từ hộp thoại đăng nhập (trả JSON) hoặc form thường.
        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            string? error = null;
            Account? acc = null;

            if (!ModelState.IsValid)
            {
                error = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
            }
            else
            {
                acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Username == model.Username);
                // Thông báo chung chung: không tiết lộ "sai tên" hay "sai mật khẩu"
                if (acc == null || !_accounts.Verify(acc, model.Password, out var rehash))
                {
                    error = "Tên đăng nhập hoặc mật khẩu không đúng";
                    acc = null;
                }
                else if (!acc.IsActive)
                {
                    error = "Tài khoản đã bị khóa";
                    acc = null;
                }
                else if (rehash)
                {
                    acc.Password = _accounts.Hash(acc, model.Password);
                    await _context.SaveChangesAsync();
                }
            }

            if (acc == null)
            {
                if (Request.IsAjax()) return Json(new { success = false, message = error });
                ModelState.AddModelError(string.Empty, error ?? "Đăng nhập không thành công");
                return View(model);
            }

            await SignInAsync(acc, model.RememberMe);

            // Chỉ chuyển hướng tới URL nội bộ -> chống Open Redirect
            var local = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            string target = IsStaff(acc.Role)
                ? (local ?? Url.Action("Index", "Home")!)
                : (local ?? Url.Action("Index", "KhachHang")!);
            // Nhân viên đăng nhập từ trang Khách thì vào thẳng trang quản trị
            if (IsStaff(acc.Role) && Request.IsAjax()) target = Url.Action("Index", "Home")!;

            if (Request.IsAjax()) return Json(new { success = true, redirect = target });
            return LocalRedirect(target);
        }

        // Đăng ký tài khoản độc giả (gọi bằng AJAX từ hộp thoại đăng ký)
        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            var pwErr = AccountService.CheckStrength(model.Password);
            if (pwErr != null && ModelState.GetValidationState(nameof(model.Password)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                ModelState.AddModelError(nameof(model.Password), pwErr);
            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await _context.Accounts.AnyAsync(a => a.Username == model.Username))
                ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại");

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return Json(new { success = false, errors });
            }

            // Vai trò luôn do server gán là "Độc giả" -> không ai tự đăng ký thành Admin được
            var acc = new Account { Username = model.Username.Trim(), Role = AppRoles.DocGia, IsActive = true };
            acc.Password = _accounts.Hash(acc, model.Password);

            await using var tx = await _context.Database.BeginTransactionAsync();
            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();

            // Hồ sơ độc giả được tạo tự động từ thông tin đăng ký
            _context.Readers.Add(new Reader
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.Phone.Trim(),
                Address = model.Address.Trim(),
                AccountId = acc.Id
            });
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            await SignInAsync(acc, false);
            return Json(new { success = true, redirect = Url.Action("Index", "KhachHang") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "KhachHang");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        private async Task SignInAsync(Account acc, bool remember)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, acc.Id.ToString()),
                new(ClaimTypes.Name, acc.Username),
                new(ClaimTypes.Role, acc.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = remember,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(remember ? 24 * 7 : 8)
                });
        }
    }
}
