using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class SachController : Controller
    {
        private const int PageSize = 8;
        private readonly LibraryDbContext _context;
        private readonly ImageUploadService _images;
        private readonly IHtmlSanitizer _sanitizer;

        public SachController(LibraryDbContext context, ImageUploadService images, IHtmlSanitizer sanitizer)
        {
            _context = context;
            _images = images;
            _sanitizer = sanitizer;
        }

        // Danh sách: phân trang + lọc (từ khóa, thể loại) + sắp xếp. Gọi bằng AJAX -> chỉ trả partial.
        public async Task<IActionResult> Index(string? search, string? category, string? sort, int page = 1)
        {
            var q = _context.Books.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(b => b.Title.Contains(search) || b.Author.Contains(search));
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(b => b.Category == category);

            q = sort switch
            {
                "title_desc" => q.OrderByDescending(b => b.Title),
                "qty" => q.OrderBy(b => b.Quantity),
                "qty_desc" => q.OrderByDescending(b => b.Quantity),
                "id_desc" => q.OrderByDescending(b => b.Id),
                _ => q.OrderBy(b => b.Title)
            };

            var model = await PaginatedList<Book>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Sort = sort;

            if (Request.IsAjax()) return PartialView("_BookTable", model);

            ViewBag.Categories = await _context.Books.Where(b => b.Category != null && b.Category != "")
                .Select(b => b.Category!).Distinct().OrderBy(c => c).ToListAsync();
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new Book { Quantity = 1 });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book model, IFormFile? coverFile)
        {
            model.CoverImage = null; // không cho client tự đặt đường dẫn ảnh
            if (coverFile != null && coverFile.Length > 0)
            {
                var (path, error) = await _images.SaveAsync(coverFile);
                if (error != null) ModelState.AddModelError("coverFile", error);
                else model.CoverImage = path;
            }
            if (!ModelState.IsValid)
            {
                _images.Delete(model.CoverImage);
                return View(model);
            }

            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : _sanitizer.Sanitize(model.Description);
            _context.Books.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm sách mới";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            return View(book);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Book model, IFormFile? coverFile, bool removeCover = false)
        {
            if (id != model.Id) return BadRequest();
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            string? newCover = null;
            if (coverFile != null && coverFile.Length > 0)
            {
                var (path, error) = await _images.SaveAsync(coverFile);
                if (error != null) ModelState.AddModelError("coverFile", error);
                else newCover = path;
            }
            if (!ModelState.IsValid)
            {
                _images.Delete(newCover);
                model.CoverImage = book.CoverImage;
                return View(model);
            }

            // Chỉ cập nhật các trường cho phép (chống over-posting)
            book.Title = model.Title;
            book.Author = model.Author;
            book.Category = model.Category;
            book.Quantity = model.Quantity;
            book.Description = string.IsNullOrWhiteSpace(model.Description) ? null : _sanitizer.Sanitize(model.Description);

            if (newCover != null)
            {
                _images.Delete(book.CoverImage);
                book.CoverImage = newCover;
            }
            else if (removeCover)
            {
                _images.Delete(book.CoverImage);
                book.CoverImage = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật sách";
            return RedirectToAction(nameof(Index));
        }

        // Xóa bằng POST + anti-forgery (không xóa bằng link GET). Hỗ trợ cả AJAX.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book != null && await _context.BorrowRecords.AnyAsync(r => r.BookId == id &&
                    (r.Status == BorrowStatus.Pending || r.Status == BorrowStatus.Borrowing)))
            {
                const string msg = "Không thể xóa: sách đang có phiếu mượn chưa hoàn tất";
                if (Request.IsAjax()) return Json(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index));
            }
            if (book != null)
            {
                _images.Delete(book.CoverImage);
                _context.Books.Remove(book);
                await _context.SaveChangesAsync();
            }
            if (Request.IsAjax()) return Json(new { success = true });
            return RedirectToAction(nameof(Index));
        }
    }
}
