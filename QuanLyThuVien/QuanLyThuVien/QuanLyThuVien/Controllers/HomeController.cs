using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class HomeController : Controller
    {
        private readonly LibraryDbContext _context;

        public HomeController(LibraryDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var now = DateTime.Now;
            ViewBag.TotalBooks = _context.Books.Count();
            ViewBag.TotalReaders = _context.Readers.Count();
            ViewBag.ActiveBorrows = _context.BorrowRecords.Count(b => b.Status == BorrowStatus.Borrowing);
            ViewBag.PendingRequests = _context.BorrowRecords.Count(b => b.Status == BorrowStatus.Pending);
            ViewBag.OverdueBorrows = _context.BorrowRecords.Count(b =>
                b.Status == BorrowStatus.Borrowing && b.DueDate.HasValue && b.DueDate.Value < now);
            return View();
        }

        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
