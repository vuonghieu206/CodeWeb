using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers.Api
{
    /// <summary>
    /// API Mượn trả (chỉ Admin/Thủ thư).
    ///   GET /api/borrows?q=&amp;status=&amp;page=&amp;pageSize=
    ///   GET /api/borrows/{id}
    ///   POST /api/borrows/{id}/approve | /reject | /return   (đổi trạng thái phiếu)
    /// </summary>
    [ApiController]
    [Route("api/borrows")]
    [Authorize(Roles = AppRoles.Staff)]
    public class BorrowsApiController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        private readonly LateFeeService _fees;
        public BorrowsApiController(LibraryDbContext context, LateFeeService fees)
        {
            _context = context;
            _fees = fees;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);
            var query = _context.BorrowRecords.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(r => r.BookName.Contains(q) || r.ReaderName.Contains(q));
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var r = await _context.BorrowRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return r == null ? NotFound() : Ok(r);
        }

        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null) return NotFound();
            if (r.Status != BorrowStatus.Pending) return Conflict(new { message = "Chỉ duyệt được phiếu đang chờ duyệt" });

            var book = r.BookId.HasValue ? await _context.Books.FindAsync(r.BookId.Value) : null;
            if (book != null)
            {
                if (book.Quantity <= 0) return Conflict(new { message = "Sách đã hết" });
                book.Quantity--;
            }
            r.Status = BorrowStatus.Borrowing;
            r.BorrowDate = DateTime.Now;
            r.DueDate = DateTime.Now.AddDays(14);
            await _context.SaveChangesAsync();
            return Ok(r);
        }

        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> Reject(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null) return NotFound();
            if (r.Status != BorrowStatus.Pending) return Conflict(new { message = "Chỉ từ chối được phiếu đang chờ duyệt" });
            r.Status = BorrowStatus.Rejected;
            await _context.SaveChangesAsync();
            return Ok(r);
        }

        [HttpPost("{id:int}/return")]
        public async Task<IActionResult> Return(int id)
        {
            var r = await _context.BorrowRecords.FindAsync(id);
            if (r == null) return NotFound();
            if (r.Status != BorrowStatus.Borrowing) return Conflict(new { message = "Phiếu này không ở trạng thái đang mượn" });

            var now = DateTime.Now;
            r.Status = BorrowStatus.Returned;
            r.ReturnDate = now;
            if (r.BookId.HasValue)
            {
                var book = await _context.Books.FindAsync(r.BookId.Value);
                if (book != null) book.Quantity++;
            }
            await _fees.UpsertAsync(r, LateFeeService.OverdueDays(r, now));
            await _context.SaveChangesAsync();
            return Ok(r);
        }
    }
}
