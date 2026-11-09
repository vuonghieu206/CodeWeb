using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Models;
using System.Linq;

namespace QuanLyThuVien.Controllers
{
    public class TheLoaiController : Controller
    {
        private readonly LibraryDbContext _context;

        public TheLoaiController(LibraryDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            // 1. Thống kê số lượng theo thể loại truyền ra ViewBag
            ViewBag.TheLoaiStats = _context.Books
                .GroupBy(b => b.Category)
                .Select(g => new {
                    CategoryName = string.IsNullOrEmpty(g.Key) ? "Chưa phân loại" : g.Key,
                    Count = g.Sum(b => b.Quantity)
                }).ToList();

            // 2. Lọc sách theo từ khóa tìm kiếm
            var books = from b in _context.Books select b;
            if (!string.IsNullOrEmpty(searchString))
            {
                books = books.Where(s => s.Category.Contains(searchString) || s.Title.Contains(searchString));
            }

            return View(books.ToList());
        }
    }
}