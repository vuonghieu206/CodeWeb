using Ganss.Xss;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class SachController : StaffController
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

            PaginatedList<Book> model;
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Khi có từ khóa tìm kiếm, hiển thị toàn bộ kết quả, không phân trang.
                var allResults = await q.ToListAsync();
                model = new PaginatedList<Book>(allResults, allResults.Count, 1, Math.Max(allResults.Count, 1));
                ViewBag.DisablePager = true;
            }
            else
            {
                // Danh sách, lọc theo thể loại và sắp xếp vẫn phân trang.
                model = await PaginatedList<Book>.CreateAsync(q, page, PageSize);
                ViewBag.DisablePager = false;
            }

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Sort = sort;

            if (Request.IsAjax()) return PartialView("_BookTable", model);

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
            return View(model);
        }

        // Danh sách thể loại cho ô chọn trong form thêm/sửa sách (lấy từ mục Danh mục)
        private async Task LoadCategoryListAsync() =>
            ViewBag.CategoryList = await _context.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

        // Thể loại bắt buộc phải là danh mục đã có
        private async Task ValidateCategoryAsync(Book model)
        {
            if (string.IsNullOrWhiteSpace(model.Category) ||
                !await _context.Categories.AnyAsync(c => c.Name == model.Category))
                ModelState.AddModelError(nameof(Book.Category), "Vui lòng chọn thể loại trong danh sách (thêm ở mục Danh mục)");
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategoryListAsync();
            return View(new Book { Quantity = 1 });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book model, IFormFile? coverFile)
        {
            model.CoverImage = null; // không cho client tự đặt đường dẫn ảnh
            await ValidateCategoryAsync(model);
            if (coverFile != null && coverFile.Length > 0)
            {
                var (path, error) = await _images.SaveAsync(coverFile);
                if (error != null) ModelState.AddModelError("coverFile", error);
                else model.CoverImage = path;
            }
            if (!ModelState.IsValid)
            {
                _images.Delete(model.CoverImage);
                await LoadCategoryListAsync();
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
            await LoadCategoryListAsync();
            return View(book);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Book model, IFormFile? coverFile, bool removeCover = false)
        {
            if (id != model.Id) return BadRequest();
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            await ValidateCategoryAsync(model);
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
                await LoadCategoryListAsync();
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
