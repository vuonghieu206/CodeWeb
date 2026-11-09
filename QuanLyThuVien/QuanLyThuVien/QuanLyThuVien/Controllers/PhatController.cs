using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class PhatController : Controller
    {
        private const int PageSize = 10;
        private readonly LibraryDbContext _context;
        private readonly LateFeeService _fees;
        public PhatController(LibraryDbContext context, LateFeeService fees)
        {
            _context = context;
            _fees = fees;
        }

        public async Task<IActionResult> Index(string? search, string? paid, int page = 1)
        {
            // Mở trang là cập nhật ngay phạt quá hạn (ngoài tiến trình chạy nền 10 phút/lần)
            if (!Request.IsAjax()) await _fees.SyncOverdueAsync();

            var q = _context.Penalties.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(p => p.ReaderName.Contains(search) || p.Reason.Contains(search));
            if (paid == "yes") q = q.Where(p => p.IsPaid);
            else if (paid == "no") q = q.Where(p => !p.IsPaid);
            q = q.OrderByDescending(p => p.Id);

            var model = await PaginatedList<Penalty>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            ViewBag.Paid = paid;
            ViewBag.TotalUnpaid = await _context.Penalties.Where(p => !p.IsPaid).SumAsync(p => (decimal?)p.Amount) ?? 0;
            if (Request.IsAjax()) return PartialView("_Table", model);
            return View(model);
        }

        [HttpGet] public IActionResult Create() => View(new Penalty());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Penalty model)
        {
            if (!ModelState.IsValid) return View(model);
            _context.Penalties.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm khoản phạt";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var penalty = await _context.Penalties.FindAsync(id);
            return penalty == null ? NotFound() : View(penalty);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Penalty model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);
            var p = await _context.Penalties.FindAsync(id);
            if (p == null) return NotFound();
            p.ReaderName = model.ReaderName;
            p.Reason = model.Reason;
            p.Amount = model.Amount;
            p.IsPaid = model.IsPaid;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật khoản phạt";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePaid(int id)
        {
            var p = await _context.Penalties.FindAsync(id);
            if (p != null) { p.IsPaid = !p.IsPaid; await _context.SaveChangesAsync(); }
            if (Request.IsAjax()) return Json(new { success = true, isPaid = p?.IsPaid });
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.Penalties.FindAsync(id);
            if (p != null) { _context.Penalties.Remove(p); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}
