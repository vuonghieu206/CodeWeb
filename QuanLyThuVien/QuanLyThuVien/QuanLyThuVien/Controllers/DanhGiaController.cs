using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    // Quản trị đánh giá của độc giả: xem, lọc, xóa đánh giá không phù hợp
    public class DanhGiaController : StaffController
    {
        private const int PageSize = 10;
        private readonly LibraryDbContext _context;

        public DanhGiaController(LibraryDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string? search, int? rating, int page = 1)
        {
            var q = _context.Reviews.AsNoTracking().AsQueryable();
            if (rating is >= 1 and <= 5) q = q.Where(r => r.Rating == rating);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var bookIds = _context.Books.Where(b => b.Title.Contains(search)).Select(b => b.Id);
                q = q.Where(r => r.UserName.Contains(search) || (r.Comment != null && r.Comment.Contains(search)) || bookIds.Contains(r.BookId));
            }
            q = q.OrderByDescending(r => r.CreatedAt);

            var model = await PaginatedList<Review>.CreateAsync(q, page, PageSize);
            var ids = model.Items.Select(r => r.BookId).Distinct().ToList();
            ViewBag.Titles = await _context.Books.Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Title);
            ViewBag.Search = search;
            ViewBag.Rating = rating;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var r = await _context.Reviews.FindAsync(id);
            if (r != null)
            {
                _context.Reviews.Remove(r);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa đánh giá";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
