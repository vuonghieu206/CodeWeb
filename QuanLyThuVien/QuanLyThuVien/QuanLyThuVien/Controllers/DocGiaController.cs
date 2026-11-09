using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class DocGiaController : Controller
    {
        private const int PageSize = 10;
        private readonly LibraryDbContext _context;

        public DocGiaController(LibraryDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var q = _context.Readers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(r => r.FullName.Contains(search) || r.PhoneNumber.Contains(search) ||
                                 (r.Email != null && r.Email.Contains(search)));
            q = q.OrderBy(r => r.FullName);

            var model = await PaginatedList<Reader>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            if (Request.IsAjax()) return PartialView("_Table", model);
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new Reader());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reader model)
        {
            if (!ModelState.IsValid) return View(model);
            _context.Readers.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm độc giả";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reader = await _context.Readers.FindAsync(id);
            if (reader == null) return NotFound();
            return View(reader);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Reader model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);
            var reader = await _context.Readers.FindAsync(id);
            if (reader == null) return NotFound();

            reader.FullName = model.FullName;
            reader.PhoneNumber = model.PhoneNumber;
            reader.Email = model.Email;
            reader.Address = model.Address;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật độc giả";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var reader = await _context.Readers.FindAsync(id);
            if (reader != null && await _context.BorrowRecords.AnyAsync(r => r.ReaderName == reader.FullName &&
                    (r.Status == BorrowStatus.Pending || r.Status == BorrowStatus.Borrowing)))
            {
                const string msg = "Không thể xóa: độc giả đang có phiếu mượn chưa hoàn tất";
                if (Request.IsAjax()) return Json(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index));
            }
            if (reader != null)
            {
                _context.Readers.Remove(reader);
                await _context.SaveChangesAsync();
            }
            if (Request.IsAjax()) return Json(new { success = true });
            return RedirectToAction(nameof(Index));
        }
    }
}
