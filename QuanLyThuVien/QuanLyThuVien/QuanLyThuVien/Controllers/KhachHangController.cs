using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    public class BorrowRequestViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        public string Phone { get; set; } = string.Empty;
    }

    public class LookupViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        public string Phone { get; set; } = string.Empty;
    }

    // Khu vực dành cho KHÁCH: không cần đăng nhập, chỉ ĐỌC dữ liệu sách và GỬI yêu cầu mượn.
    [AllowAnonymous]
    public class KhachHangController : Controller
    {
        private const int PageSize = 8;
        private const int MaxCartItems = 5;
        private const string CartKey = "cart";
        private const string RecentCookie = "recent_books";

        private readonly LibraryDbContext _context;
        private readonly IStringLocalizer<SharedResource> _L;

        public KhachHangController(LibraryDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _L = localizer;
        }

        // Trang chủ + tra cứu: tìm kiếm, lọc thể loại, sắp xếp, phân trang, tải bằng AJAX
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
                "newest" => q.OrderByDescending(b => b.Id),
                "available" => q.OrderByDescending(b => b.Quantity),
                _ => q.OrderBy(b => b.Title)
            };

            var model = await PaginatedList<Book>.CreateAsync(q, page, PageSize);
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Sort = sort;
            if (Request.IsAjax()) return PartialView("_BookCards", model);

            ViewBag.Categories = await _context.Books.Where(b => b.Category != null && b.Category != "")
                .Select(b => b.Category!).Distinct().OrderBy(c => c).ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (book == null) return NotFound();

            // Cookie "sách đã xem gần đây" (tối đa 5 cuốn, lưu 30 ngày)
            var recent = ReadRecent();
            recent.Remove(id);
            recent.Insert(0, id);
            recent = recent.Take(5).ToList();
            Response.Cookies.Append(RecentCookie, string.Join(',', recent), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30), IsEssential = true
            });

            var otherIds = recent.Where(x => x != id).ToList();
            var viewed = await _context.Books.AsNoTracking().Where(b => otherIds.Contains(b.Id)).ToListAsync();
            ViewBag.Recent = otherIds.Select(i => viewed.FirstOrDefault(b => b.Id == i)).Where(b => b != null).ToList();
            ViewBag.InCart = GetCart().Contains(id);
            return View(book);
        }

        private List<int> ReadRecent()
        {
            var raw = Request.Cookies[RecentCookie];
            if (string.IsNullOrEmpty(raw)) return new List<int>();
            // Dữ liệu cookie là do client gửi -> luôn parse an toàn
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0).Where(n => n > 0).Distinct().Take(5).ToList();
        }

        // ---------- Giỏ mượn (lưu trong Session) ----------
        private List<int> GetCart() => HttpContext.Session.GetObject<List<int>>(CartKey) ?? new List<int>();

        public async Task<IActionResult> Cart()
        {
            var ids = GetCart();
            var books = await _context.Books.AsNoTracking().Where(b => ids.Contains(b.Id)).ToListAsync();
            // dọn các sách đã bị xóa khỏi DB
            if (books.Count != ids.Count) HttpContext.Session.SetObject(CartKey, books.Select(b => b.Id).ToList());
            return View(books);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int id)
        {
            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (book == null) return Json(new { success = false, message = _L["Không tìm thấy sách"].Value });
            if (book.Quantity <= 0) return Json(new { success = false, message = _L["Sách này đã hết"].Value });

            var cart = GetCart();
            if (cart.Contains(id)) return Json(new { success = false, count = cart.Count, message = _L["Sách đã có trong giỏ"].Value });
            if (cart.Count >= MaxCartItems)
                return Json(new { success = false, count = cart.Count, message = string.Format(_L["Mỗi lần chỉ mượn tối đa {0} cuốn"].Value, MaxCartItems) });

            cart.Add(id);
            HttpContext.Session.SetObject(CartKey, cart);
            return Json(new { success = true, count = cart.Count, message = _L["Đã thêm vào giỏ mượn"].Value });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            cart.Remove(id);
            HttpContext.Session.SetObject(CartKey, cart);
            return RedirectToAction(nameof(Cart));
        }

        // Gửi yêu cầu mượn: tạo phiếu ở trạng thái "Chờ duyệt" để thủ thư xử lý
        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("checkout")]
        public async Task<IActionResult> Checkout(BorrowRequestViewModel model)
        {
            var ids = GetCart();
            var books = await _context.Books.Where(b => ids.Contains(b.Id)).ToListAsync();

            if (books.Count == 0)
            {
                TempData["Error"] = _L["Giỏ mượn đang trống"].Value;
                return RedirectToAction(nameof(Cart));
            }
            if (!ModelState.IsValid)
            {
                ViewBag.Form = model;
                return View(nameof(Cart), books.AsEnumerable());
            }

            foreach (var b in books.Where(b => b.Quantity > 0))
            {
                _context.BorrowRecords.Add(new BorrowRecord
                {
                    BookId = b.Id,
                    BookName = b.Title,
                    ReaderName = model.FullName.Trim(),
                    ContactPhone = model.Phone.Trim(),
                    BorrowDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(14),
                    Status = BorrowStatus.Pending
                });
            }
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(CartKey);
            TempData["Success"] = _L["Đã gửi yêu cầu mượn sách. Thủ thư sẽ liên hệ với bạn qua số điện thoại."].Value;
            return Redirect("/KhachHang");
        }

        // ---------- Tra cứu trạng thái yêu cầu mượn ----------
        // Phải khớp CẢ họ tên lẫn số điện thoại đã dùng khi gửi yêu cầu mới xem được (tránh lộ dữ liệu người khác)
        [HttpGet]
        public IActionResult TraCuu() => View(new LookupViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("lookup")]
        public async Task<IActionResult> TraCuu(LookupViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var name = model.FullName.Trim();
            var phone = model.Phone.Trim();
            ViewBag.Records = await _context.BorrowRecords.AsNoTracking()
                .Where(r => r.ReaderName == name && r.ContactPhone == phone)
                .OrderByDescending(r => r.Id)
                .Take(50)
                .ToListAsync();
            return View(model);
        }

        // Đổi ngôn ngữ: ghi cookie culture; returnUrl chỉ nhận URL nội bộ (chống Open Redirect)
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string? returnUrl)
        {
            if (culture != "vi" && culture != "en") culture = "vi";
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }
    }
}
