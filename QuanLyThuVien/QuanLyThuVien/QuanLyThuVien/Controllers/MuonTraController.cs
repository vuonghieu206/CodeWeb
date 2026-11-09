using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class MuonTraController : Controller
    {
        private const int PageSize = 10;
        private const int LoanDays = 14;
        private readonly LibraryDbContext _context;
        private readonly LateFeeService _fees;

        public MuonTraController(LibraryDbContext context, LateFeeService fees)
        {
            _context = context;
            _fees = fees;
        }

        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var q = _context.BorrowRecords.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(r => r.BookName.Contains(search) || r.ReaderName.Contains(search));
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(r => r.Status == status);
            q = q.OrderByDescending(r => r.Id);

            var model = await PaginatedList<BorrowRecord>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;
            if (Request.IsAjax()) return PartialView("_Table", model);
            return View(model);
        }

        private async Task LoadListsAsync()
        {
            ViewBag.Books = new SelectList(
                await _context.Books.Where(b => b.Quantity > 0).OrderBy(b => b.Title).ToListAsync(), "Id", "Title");
            ViewBag.Readers = await _context.Readers.OrderBy(r => r.FullName).Select(r => r.FullName).ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadListsAsync();
            return View(new BorrowRecord { BorrowDate = DateTime.Today, DueDate = DateTime.Today.AddDays(LoanDays) });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BorrowRecord model)
        {
            ModelState.Remove(nameof(BorrowRecord.BookName)); // tên sách lấy từ DB, không tin client
            var book = model.BookId.HasValue ? await _context.Books.FindAsync(model.BookId.Value) : null;
            if (book == null) ModelState.AddModelError(nameof(BorrowRecord.BookId), "Vui lòng chọn sách");
            else if (book.Quantity <= 0) ModelState.AddModelError(nameof(BorrowRecord.BookId), "Sách này đã hết");

            if (model.DueDate.HasValue && model.DueDate.Value.Date < model.BorrowDate.Date)
                ModelState.AddModelError(nameof(BorrowRecord.DueDate), "Hạn trả phải sau ngày mượn");

            if (!ModelState.IsValid || book == null)
            {
                await LoadListsAsync();
                return View(model);
            }

            model.BookName = book.Title;
            model.Status = BorrowStatus.Borrowing;
            model.ReturnDate = null;
            model.DueDate ??= model.BorrowDate.AddDays(LoanDays);
            book.Quantity--;
            _context.BorrowRecords.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã tạo phiếu mượn";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null) return NotFound();
            return View(r);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BorrowRecord model)
        {
            if (id != model.Id) return BadRequest();
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null) return NotFound();

            // Các trường không sửa ở form này -> bỏ qua kiểm tra
            ModelState.Remove(nameof(BorrowRecord.BookName));
            if (model.DueDate.HasValue && model.DueDate.Value.Date < r.BorrowDate.Date)
                ModelState.AddModelError(nameof(BorrowRecord.DueDate), "Hạn trả phải sau ngày mượn");
            if (!ModelState.IsValid)
            {
                model.BookName = r.BookName; model.BorrowDate = r.BorrowDate; model.Status = r.Status;
                return View(model);
            }

            r.ReaderName = model.ReaderName;
            r.ContactPhone = model.ContactPhone;
            r.DueDate = model.DueDate;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật phiếu mượn";
            return RedirectToAction(nameof(Index));
        }

        // Duyệt yêu cầu mượn do Khách gửi: Chờ duyệt -> Đang mượn (trừ kho)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null || r.Status != BorrowStatus.Pending) return RedirectToAction(nameof(Index));

            var book = r.BookId.HasValue ? await _context.Books.FindAsync(r.BookId.Value) : null;
            if (book != null)
            {
                if (book.Quantity <= 0)
                {
                    TempData["Error"] = $"Sách \"{book.Title}\" đã hết, không thể duyệt";
                    return RedirectToAction(nameof(Index));
                }
                book.Quantity--;
            }
            r.Status = BorrowStatus.Borrowing;
            r.BorrowDate = DateTime.Now;
            r.DueDate = DateTime.Now.AddDays(LoanDays);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt yêu cầu mượn";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r != null && r.Status == BorrowStatus.Pending)
            {
                r.Status = BorrowStatus.Rejected;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã từ chối yêu cầu";
            }
            return RedirectToAction(nameof(Index));
        }

        // Nhận trả sách: Đang mượn -> Đã trả (cộng lại kho)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r != null && r.Status == BorrowStatus.Borrowing)
            {
                var now = DateTime.Now;
                r.Status = BorrowStatus.Returned;
                r.ReturnDate = now;
                if (r.BookId.HasValue)
                {
                    var book = await _context.Books.FindAsync(r.BookId.Value);
                    if (book != null) book.Quantity++;
                }

                TempData["Success"] = "Đã nhận trả sách";
                var days = LateFeeService.OverdueDays(r, now);
                if (days > 0)
                {
                    // Chốt khoản phạt theo số ngày trễ thực tế (cập nhật khoản phạt tự động nếu đã có)
                    await _fees.UpsertAsync(r, days);
                    TempData["Success"] = $"Đã nhận trả sách. Trả trễ {days} ngày, tiền phạt {days * LateFeeService.FeePerDay:N0} đ (đã ghi vào mục Tiền phạt).";
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r != null)
            {
                // Xóa phiếu đang mượn thì trả sách về kho
                if (r.Status == BorrowStatus.Borrowing && r.BookId.HasValue)
                {
                    var book = await _context.Books.FindAsync(r.BookId.Value);
                    if (book != null) book.Quantity++;
                }
                _context.BorrowRecords.Remove(r);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
