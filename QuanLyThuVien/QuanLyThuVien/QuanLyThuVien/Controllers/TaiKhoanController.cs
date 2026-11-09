using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    // Chỉ Admin được quản lý tài khoản (Thủ thư bị chặn -> 403)
    [Authorize(Roles = AppRoles.Admin)]
    public class TaiKhoanController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly AccountService _accounts;

        public TaiKhoanController(LibraryDbContext context, AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        private int CurrentId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private static bool ValidRole(string? r) => r == AppRoles.Admin || r == AppRoles.ThuThu;

        private static string? CheckPassword(string? pw)
        {
            if (string.IsNullOrEmpty(pw) || pw.Length < 8) return "Mật khẩu tối thiểu 8 ký tự";
            if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return "Mật khẩu phải có cả chữ và số";
            return null;
        }

        public async Task<IActionResult> Index() =>
            View(await _context.Accounts.AsNoTracking().OrderBy(a => a.Username).ToListAsync());

        [HttpGet] public IActionResult Create() => View(new Account());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account model)
        {
            if (!ValidRole(model.Role)) ModelState.AddModelError(nameof(Account.Role), "Vai trò không hợp lệ");
            var pwErr = CheckPassword(model.Password);
            if (pwErr != null) ModelState.AddModelError(nameof(Account.Password), pwErr);
            if (await _context.Accounts.AnyAsync(a => a.Username == model.Username))
                ModelState.AddModelError(nameof(Account.Username), "Tên đăng nhập đã tồn tại");
            if (!ModelState.IsValid) { model.Password = ""; return View(model); }

            var acc = new Account { Username = model.Username, Role = model.Role, IsActive = model.IsActive };
            acc.Password = _accounts.Hash(acc, model.Password);
            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã tạo tài khoản";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();
            a.Password = ""; // KHÔNG đưa hash ra form
            return View(a);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account model)
        {
            if (id != model.Id) return BadRequest();
            var acc = await _context.Accounts.FindAsync(id);
            if (acc == null) return NotFound();

            ModelState.Remove(nameof(Account.Password)); // để trống = giữ mật khẩu cũ
            if (!ValidRole(model.Role)) ModelState.AddModelError(nameof(Account.Role), "Vai trò không hợp lệ");
            if (!string.IsNullOrEmpty(model.Password))
            {
                var pwErr = CheckPassword(model.Password);
                if (pwErr != null) ModelState.AddModelError(nameof(Account.Password), pwErr);
            }
            if (await _context.Accounts.AnyAsync(a => a.Username == model.Username && a.Id != id))
                ModelState.AddModelError(nameof(Account.Username), "Tên đăng nhập đã tồn tại");

            // Không cho tự hạ quyền / tự khóa chính mình, và luôn còn ít nhất 1 admin hoạt động
            if (id == CurrentId && (!model.IsActive || model.Role != AppRoles.Admin))
                ModelState.AddModelError(string.Empty, "Bạn không thể tự khóa hoặc hạ quyền tài khoản đang đăng nhập");

            if (!ModelState.IsValid) { model.Password = ""; return View(model); }

            acc.Username = model.Username;
            acc.Role = model.Role;
            acc.IsActive = model.IsActive;
            if (!string.IsNullOrEmpty(model.Password)) acc.Password = _accounts.Hash(acc, model.Password);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật tài khoản";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (id == CurrentId)
            {
                TempData["Error"] = "Không thể xóa tài khoản đang đăng nhập";
                return RedirectToAction(nameof(Index));
            }
            var acc = await _context.Accounts.FindAsync(id);
            if (acc != null)
            {
                _context.Accounts.Remove(acc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài khoản";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
