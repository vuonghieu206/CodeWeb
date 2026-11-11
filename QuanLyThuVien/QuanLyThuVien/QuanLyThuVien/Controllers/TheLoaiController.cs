using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class TheLoaiController : StaffController
    {
        private const int PageSize = 8;
        private readonly LibraryDbContext _context;

        public TheLoaiController(LibraryDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            ViewData["CurrentFilter"] = searchString;

            // Thống kê số lượng sách theo thể loại
            ViewBag.TheLoaiStats = await _context.Books
                .GroupBy(b => b.Category)
                .Select(g => new
                {
                    CategoryName = g.Key == null || g.Key == "" ? "Chưa phân loại" : g.Key,
                    Count = g.Sum(b => b.Quantity)
                }).ToListAsync();

            var books = _context.Books.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchString))
                books = books.Where(s => (s.Category != null && s.Category.Contains(searchString)) || s.Title.Contains(searchString));

            var model = await PaginatedList<Book>.CreateAsync(books.OrderBy(b => b.Title), page, PageSize);
            return View(model);
        }
    }
}
