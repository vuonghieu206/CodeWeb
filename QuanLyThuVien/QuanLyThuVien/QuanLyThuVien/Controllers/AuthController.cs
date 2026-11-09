using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public class AuthController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly AccountService _accounts;
        public AuthController(LibraryDbContext context, AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.Username == model.Username);

            // Thông báo chung chung: không tiết lộ "sai tên" hay "sai mật khẩu"
            if (acc == null || !_accounts.Verify(acc, model.Password, out var rehash))
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
                return View(model);
            }
            if (!acc.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa");
                return View(model);
            }
            if (rehash)
            {
                acc.Password = _accounts.Hash(acc, model.Password);
                await _context.SaveChangesAsync();
            }

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
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 * 7 : 8)
                });

            // Cookie hiển thị tên (không nhạy cảm) - minh họa dùng cookie
            Response.Cookies.Append("last_user", acc.Username, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            // Chỉ chuyển hướng tới URL nội bộ -> chống Open Redirect
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
