using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    // Chỉ Admin được quản lý tài khoản (Thủ thư bị chặn -> 403)
    [Authorize(Roles = AppRoles.Admin)]
    public class TaiKhoanController : StaffController
    {
        private const int PageSize = 10;
        private readonly LibraryDbContext _context;
        private readonly AccountService _accounts;

        public TaiKhoanController(LibraryDbContext context, AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        private int CurrentId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private static bool IsStaffRole(string? r) => r == AppRoles.Admin || r == AppRoles.ThuThu;

        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            var q = _context.Accounts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(a => a.Username.Contains(search));
            if (!string.IsNullOrWhiteSpace(role)) q = q.Where(a => a.Role == role);
            q = q.OrderBy(a => a.Role).ThenBy(a => a.Username);

            var model = await PaginatedList<Account>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            ViewBag.Role = role;
            return View(model);
        }

        [HttpGet] public IActionResult Create() => View(new Account());

        // Admin chỉ tạo tài khoản nhân viên; tài khoản độc giả do khách tự đăng ký trên website
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account model)
        {
            if (!IsStaffRole(model.Role)) ModelState.AddModelError(nameof(Account.Role), "Vai trò không hợp lệ");
            var pwErr = AccountService.CheckStrength(model.Password);
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

            var isReader = acc.Role == AppRoles.DocGia;
            if (isReader) model.Role = AppRoles.DocGia;   // không đổi vai trò tài khoản độc giả

            ModelState.Remove(nameof(Account.Password)); // để trống = giữ mật khẩu cũ
            if (!isReader && !IsStaffRole(model.Role)) ModelState.AddModelError(nameof(Account.Role), "Vai trò không hợp lệ");
            if (!string.IsNullOrEmpty(model.Password))
            {
                var pwErr = AccountService.CheckStrength(model.Password);
                if (pwErr != null) ModelState.AddModelError(nameof(Account.Password), pwErr);
            }
            if (await _context.Accounts.AnyAsync(a => a.Username == model.Username && a.Id != id))
                ModelState.AddModelError(nameof(Account.Username), "Tên đăng nhập đã tồn tại");

            // Không cho tự hạ quyền / tự khóa chính mình
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
                // Hồ sơ độc giả (và lịch sử mượn) được giữ lại, chỉ gỡ liên kết với tài khoản đã xóa
                var readers = await _context.Readers.Where(r => r.AccountId == id).ToListAsync();
                foreach (var r in readers) r.AccountId = null;

                _context.Accounts.Remove(acc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài khoản";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
