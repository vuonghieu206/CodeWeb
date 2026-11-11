using System.ComponentModel.DataAnnotations;
using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers.Api
{
    public class BookInput
    {
        [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(150)] public string Author { get; set; } = string.Empty;
        [Required, StringLength(100)] public string Category { get; set; } = string.Empty;
        [Range(0, 10000)] public int Quantity { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// RESTful API cho Sách.
    ///   GET    /api/books?q=&amp;category=&amp;page=&amp;pageSize=   (công khai)
    ///   GET    /api/books/{id}                           (công khai)
    ///   GET    /api/books/categories                     (công khai)
    ///   POST   /api/books                                (Admin/Thủ thư)
    ///   PUT    /api/books/{id}                           (Admin/Thủ thư)
    ///   DELETE /api/books/{id}                           (Admin/Thủ thư)
    /// </summary>
    [ApiController]
    [Route("api/books")]
    public class BooksApiController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        private readonly IHtmlSanitizer _sanitizer;

        public BooksApiController(LibraryDbContext context, IHtmlSanitizer sanitizer)
        {
            _context = context;
            _sanitizer = sanitizer;
        }

        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] string? category,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);

            var query = _context.Books.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(b => b.Title.Contains(q) || b.Author.Contains(q));
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(b => b.Category == category);

            var total = await query.CountAsync();
            var items = await query.OrderBy(b => b.Title)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(b => new { b.Id, b.Title, b.Author, b.Category, b.Quantity, b.CoverImage })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var b = await _context.Books.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return b == null ? NotFound() : Ok(b);
        }

        [HttpGet("categories"), AllowAnonymous]
        public async Task<IActionResult> GetCategories() =>
            Ok(await _context.Books.Where(b => b.Category != null && b.Category != "")
                .Select(b => b.Category).Distinct().OrderBy(c => c).ToListAsync());

        [HttpPost, Authorize(Roles = AppRoles.Staff)]
        public async Task<IActionResult> Create([FromBody] BookInput input)
        {
            if (!await _context.Categories.AnyAsync(c => c.Name == input.Category))
                return BadRequest(new { message = "Danh mục không tồn tại, hãy thêm ở mục Danh mục trước" });
            var book = new Book
            {
                Title = input.Title, Author = input.Author, Category = input.Category, Quantity = input.Quantity,
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : _sanitizer.Sanitize(input.Description)
            };
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
        }

        [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Staff)]
        public async Task<IActionResult> Update(int id, [FromBody] BookInput input)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            if (!await _context.Categories.AnyAsync(c => c.Name == input.Category))
                return BadRequest(new { message = "Danh mục không tồn tại, hãy thêm ở mục Danh mục trước" });
            book.Title = input.Title;
            book.Author = input.Author;
            book.Category = input.Category;
            book.Quantity = input.Quantity;
            book.Description = string.IsNullOrWhiteSpace(input.Description) ? null : _sanitizer.Sanitize(input.Description);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}"), Authorize(Roles = AppRoles.Staff)]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            if (await _context.BorrowRecords.AnyAsync(r => r.BookId == id &&
                    (r.Status == BorrowStatus.Pending || r.Status == BorrowStatus.Borrowing)))
                return Conflict(new { message = "Sách đang có phiếu mượn chưa hoàn tất" });
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
