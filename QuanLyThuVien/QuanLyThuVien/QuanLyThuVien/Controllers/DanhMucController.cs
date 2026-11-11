using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class DanhMucController : StaffController
    {
        private const int PageSize = 8;
        private readonly LibraryDbContext _context;

        public DanhMucController(LibraryDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var q = _context.Categories.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));
            q = q.OrderBy(c => c.Name);

            var model = await PaginatedList<Category>.CreateAsync(q, page, PageSize);

            // Số sách thuộc từng danh mục
            var counts = await _context.Books.Where(b => b.Category != null)
                .GroupBy(b => b.Category!)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count);
            ViewBag.Counts = counts;
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new Category());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();
            if (await _context.Categories.AnyAsync(c => c.Name == model.Name))
                ModelState.AddModelError(nameof(Category.Name), "Danh mục này đã tồn tại");
            if (!ModelState.IsValid) return View(model);

            _context.Categories.Add(new Category { Name = model.Name, Description = model.Description });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm danh mục";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _context.Categories.FindAsync(id);
            return c == null ? NotFound() : View(c);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.Id) return BadRequest();
            var c = await _context.Categories.FindAsync(id);
            if (c == null) return NotFound();

            model.Name = (model.Name ?? string.Empty).Trim();
            if (await _context.Categories.AnyAsync(x => x.Name == model.Name && x.Id != id))
                ModelState.AddModelError(nameof(Category.Name), "Danh mục này đã tồn tại");
            if (!ModelState.IsValid) return View(model);

            var oldName = c.Name;
            c.Name = model.Name;
            c.Description = model.Description;

            // Đổi tên danh mục thì đổi theo tên thể loại của các sách đang thuộc danh mục đó
            if (!string.Equals(oldName, c.Name, StringComparison.Ordinal))
            {
                var books = await _context.Books.Where(b => b.Category == oldName).ToListAsync();
                foreach (var b in books) b.Category = c.Name;
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật danh mục";
            return RedirectToAction(nameof(Index));
        }

        // Không cho xóa danh mục đang có sách
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _context.Categories.FindAsync(id);
            if (c == null) return RedirectToAction(nameof(Index));

            var bookCount = await _context.Books.CountAsync(b => b.Category == c.Name);
            if (bookCount > 0)
            {
                TempData["Error"] = $"Không thể xóa danh mục \"{c.Name}\": đang có {bookCount} đầu sách thuộc danh mục này";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(c);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa danh mục";
            return RedirectToAction(nameof(Index));
        }
    }
}
