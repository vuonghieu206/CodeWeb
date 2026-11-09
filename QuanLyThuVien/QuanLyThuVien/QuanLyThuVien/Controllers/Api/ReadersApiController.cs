using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers.Api
{
    /// <summary>RESTful API Độc giả - chỉ Admin/Thủ thư (dữ liệu cá nhân, không công khai).</summary>
    [ApiController]
    [Route("api/readers")]
    [Authorize(Roles = AppRoles.Staff)]
    public class ReadersApiController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        public ReadersApiController(LibraryDbContext context) { _context = context; }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);
            var query = _context.Readers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(r => r.FullName.Contains(q) || r.PhoneNumber.Contains(q));
            var total = await query.CountAsync();
            var items = await query.OrderBy(r => r.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var r = await _context.Readers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return r == null ? NotFound() : Ok(r);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Reader input)
        {
            input.Id = 0;
            _context.Readers.Add(input);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = input.Id }, input);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Reader input)
        {
            var r = await _context.Readers.FindAsync(id);
            if (r == null) return NotFound();
            r.FullName = input.FullName;
            r.PhoneNumber = input.PhoneNumber;
            r.Email = input.Email;
            r.Address = input.Address;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var r = await _context.Readers.FindAsync(id);
            if (r == null) return NotFound();
            if (await _context.BorrowRecords.AnyAsync(b => b.ReaderName == r.FullName &&
                    (b.Status == BorrowStatus.Pending || b.Status == BorrowStatus.Borrowing)))
                return Conflict(new { message = "Độc giả đang có phiếu mượn chưa hoàn tất" });
            _context.Readers.Remove(r);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
