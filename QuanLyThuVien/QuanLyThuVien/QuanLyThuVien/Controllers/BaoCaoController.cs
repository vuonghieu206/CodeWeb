using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class BaoCaoViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalCopies { get; set; }
        public int TotalReaders { get; set; }
        public int Borrowing { get; set; }
        public int Overdue { get; set; }
        public decimal UnpaidFines { get; set; }

        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryValues { get; set; } = new();
        public List<string> StatusLabels { get; set; } = new();
        public List<int> StatusValues { get; set; } = new();
        public List<string> MonthLabels { get; set; } = new();
        public List<int> MonthValues { get; set; } = new();
        public List<TopBook> TopBooks { get; set; } = new();
    }

    public class TopBook
    {
        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BaoCaoController : Controller
    {
        private readonly LibraryDbContext _context;

        public BaoCaoController(LibraryDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var vm = new BaoCaoViewModel
            {
                TotalBooks = await _context.Books.CountAsync(),
                TotalCopies = await _context.Books.SumAsync(b => (int?)b.Quantity) ?? 0,
                TotalReaders = await _context.Readers.CountAsync(),
                Borrowing = await _context.BorrowRecords.CountAsync(b => b.Status == BorrowStatus.Borrowing),
                Overdue = await _context.BorrowRecords.CountAsync(b =>
                    b.Status == BorrowStatus.Borrowing && b.DueDate.HasValue && b.DueDate.Value < now),
                UnpaidFines = await _context.Penalties.Where(p => !p.IsPaid).SumAsync(p => (decimal?)p.Amount) ?? 0
            };

            // Số lượng sách theo thể loại
            var byCategory = await _context.Books
                .GroupBy(b => b.Category)
                .Select(g => new { Category = g.Key, Count = g.Sum(b => b.Quantity) })
                .ToListAsync();
            foreach (var c in byCategory.OrderByDescending(x => x.Count))
            {
                vm.CategoryLabels.Add(string.IsNullOrEmpty(c.Category) ? "Chưa phân loại" : c.Category);
                vm.CategoryValues.Add(c.Count);
            }

            // Số phiếu theo trạng thái (luôn hiện đủ 4 trạng thái)
            var byStatus = await _context.BorrowRecords
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            foreach (var s in BorrowStatus.All)
            {
                vm.StatusLabels.Add(s);
                vm.StatusValues.Add(byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0);
            }

            // Lượt mượn 6 tháng gần nhất (không tính phiếu bị từ chối)
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
            var dates = await _context.BorrowRecords
                .Where(r => r.BorrowDate >= start && r.Status != BorrowStatus.Rejected)
                .Select(r => r.BorrowDate)
                .ToListAsync();
            for (var i = 0; i < 6; i++)
            {
                var m = start.AddMonths(i);
                vm.MonthLabels.Add(m.ToString("MM/yyyy"));
                vm.MonthValues.Add(dates.Count(d => d.Year == m.Year && d.Month == m.Month));
            }

            // Top 5 sách được mượn nhiều nhất
            vm.TopBooks = await _context.BorrowRecords
                .Where(r => r.Status != BorrowStatus.Rejected)
                .GroupBy(r => r.BookName)
                .Select(g => new TopBook { Title = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }
    }
}
