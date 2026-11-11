using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Helpers;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers
{
    public class CategoryCount
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RatingInfo
    {
        public double Average { get; set; }
        public int Count { get; set; }
    }

    public class ReviewsViewModel
    {
        public Book Book { get; set; } = null!;
        public PaginatedList<Review> Reviews { get; set; } = null!;
        public double Average { get; set; }
        public int Count { get; set; }
        public Review? Mine { get; set; }          // đánh giá hiện có của người đang đăng nhập
        public bool IsReader { get; set; }         // đang đăng nhập bằng tài khoản độc giả
        public bool IsAuthenticated { get; set; }
    }

    // Thông tin cá nhân của độc giả (trang "Tài khoản")
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // Giao diện KHÁCH. Xem sách: ai cũng xem được. Mượn sách, đánh giá, tài khoản, lịch sử: cần đăng nhập tài khoản Độc giả.
    public class KhachHangController : Controller
    {
        private const int PageSize = 8;
        private const int ReviewPageSize = 5;
        private const int MaxCartItems = 5;
        private const string CartKey = "cart";
        private const string RecentCookie = "recent_books";

        private readonly LibraryDbContext _context;
        private readonly AccountService _accounts;

        public KhachHangController(LibraryDbContext context, AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        private int CurrentAccountId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsReader => User.IsInRole(AppRoles.DocGia);

        // ---------- Trang chủ: danh mục thể loại + tìm kiếm + phân trang (AJAX) ----------
        [AllowAnonymous]
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

            // Điểm đánh giá trung bình của các sách trong trang này
            var ids = model.Items.Select(b => b.Id).ToList();
            var ratings = await _context.Reviews.Where(r => ids.Contains(r.BookId))
                .GroupBy(r => r.BookId)
                .Select(g => new { BookId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
                .ToListAsync();
            ViewBag.Ratings = ratings.ToDictionary(x => x.BookId, x => new RatingInfo { Average = x.Avg, Count = x.Count });

            if (Request.IsAjax()) return PartialView("_BookCards", model);

            // Danh mục thể loại kèm số đầu sách
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name)
                .Select(c => new CategoryCount { Name = c.Name, Count = _context.Books.Count(b => b.Category == c.Name) })
                .ToListAsync();
            ViewBag.TotalBooks = await _context.Books.CountAsync();
            return View(model);
        }

        // ---------- Chi tiết sách + đánh giá ----------
        [AllowAnonymous]
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
            ViewBag.Reviews = await BuildReviewsAsync(book, 1);
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

        private async Task<ReviewsViewModel> BuildReviewsAsync(Book book, int page)
        {
            var q = _context.Reviews.AsNoTracking().Where(r => r.BookId == book.Id);
            var count = await q.CountAsync();
            var avg = count == 0 ? 0 : await q.AverageAsync(r => (double)r.Rating);
            var list = await PaginatedList<Review>.CreateAsync(q.OrderByDescending(r => r.CreatedAt), page, ReviewPageSize);

            Review? mine = null;
            if (IsReader)
            {
                var uid = CurrentAccountId;
                mine = await q.FirstOrDefaultAsync(r => r.AccountId == uid);
            }
            return new ReviewsViewModel
            {
                Book = book, Reviews = list, Average = avg, Count = count, Mine = mine,
                IsReader = IsReader, IsAuthenticated = User.Identity?.IsAuthenticated == true
            };
        }

        // Tải trang đánh giá khác (AJAX)
        [AllowAnonymous]
        public async Task<IActionResult> Reviews(int bookId, int page = 1)
        {
            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId);
            if (book == null) return NotFound();
            return PartialView("_Reviews", await BuildReviewsAsync(book, page));
        }

        // Gửi đánh giá: mỗi độc giả một đánh giá cho mỗi sách (gửi lại sẽ cập nhật). Trả về danh sách mới để hiện ngay.
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> AddReview(int bookId, int rating, string? comment)
        {
            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId);
            if (book == null) return NotFound();
            if (rating < 1 || rating > 5) return BadRequest("Vui lòng chọn số sao từ 1 đến 5");
            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            if (comment != null && comment.Length > 1000) return BadRequest("Bình luận tối đa 1000 ký tự");

            var uid = CurrentAccountId;
            var reader = await _context.Readers.AsNoTracking().FirstOrDefaultAsync(r => r.AccountId == uid);
            var displayName = reader?.FullName ?? User.Identity?.Name ?? "Độc giả";

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.AccountId == uid);
            if (review == null)
            {
                review = new Review { BookId = bookId, AccountId = uid };
                _context.Reviews.Add(review);
            }
            review.UserName = displayName;
            review.Rating = rating;
            review.Comment = comment;
            review.CreatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return PartialView("_Reviews", await BuildReviewsAsync(book, 1));
        }

        // ---------- Giỏ mượn (Session) ----------
        private List<int> GetCart() => HttpContext.Session.GetObject<List<int>>(CartKey) ?? new List<int>();

        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> Cart()
        {
            var ids = GetCart();
            var books = await _context.Books.AsNoTracking().Where(b => ids.Contains(b.Id)).ToListAsync();
            if (books.Count != ids.Count) HttpContext.Session.SetObject(CartKey, books.Select(b => b.Id).ToList());

            var reader = await _context.Readers.AsNoTracking().FirstOrDefaultAsync(r => r.AccountId == CurrentAccountId);
            ViewBag.Reader = reader;
            return View(books);
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int id)
        {
            if (User.Identity?.IsAuthenticated != true)
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để mượn sách" });
            if (!IsReader)
                return Json(new { success = false, message = "Tài khoản quản trị không mượn sách được. Hãy dùng tài khoản độc giả." });

            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (book == null) return Json(new { success = false, message = "Không tìm thấy sách" });
            if (book.Quantity <= 0) return Json(new { success = false, message = "Sách này đã hết" });

            var cart = GetCart();
            if (cart.Contains(id)) return Json(new { success = false, count = cart.Count, message = "Sách đã có trong giỏ" });
            if (cart.Count >= MaxCartItems)
                return Json(new { success = false, count = cart.Count, message = $"Mỗi lần chỉ mượn tối đa {MaxCartItems} cuốn" });

            cart.Add(id);
            HttpContext.Session.SetObject(CartKey, cart);
            return Json(new { success = true, count = cart.Count, message = "Đã thêm vào giỏ mượn" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            cart.Remove(id);
            HttpContext.Session.SetObject(CartKey, cart);
            return RedirectToAction(nameof(Cart));
        }

        // Gửi yêu cầu mượn: thông tin người mượn lấy từ hồ sơ tài khoản, phiếu ở trạng thái "Chờ duyệt"
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        [EnableRateLimiting("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var uid = CurrentAccountId;
            var reader = await _context.Readers.FirstOrDefaultAsync(r => r.AccountId == uid);
            if (reader == null || string.IsNullOrWhiteSpace(reader.FullName) || string.IsNullOrWhiteSpace(reader.PhoneNumber))
            {
                TempData["Error"] = "Vui lòng cập nhật đầy đủ thông tin cá nhân trước khi mượn sách";
                return RedirectToAction(nameof(TaiKhoan));
            }

            var ids = GetCart();
            var books = await _context.Books.Where(b => ids.Contains(b.Id)).ToListAsync();
            if (books.Count == 0)
            {
                TempData["Error"] = "Giỏ mượn đang trống";
                return RedirectToAction(nameof(Cart));
            }

            var created = 0;
            var skipped = new List<string>();
            foreach (var b in books)
            {
                var duplicate = await _context.BorrowRecords.AnyAsync(r => r.AccountId == uid && r.BookId == b.Id &&
                    (r.Status == BorrowStatus.Pending || r.Status == BorrowStatus.Borrowing));
                if (b.Quantity <= 0 || duplicate) { skipped.Add(b.Title); continue; }

                _context.BorrowRecords.Add(new BorrowRecord
                {
                    AccountId = uid,
                    BookId = b.Id,
                    BookName = b.Title,
                    ReaderName = reader.FullName,
                    ContactPhone = reader.PhoneNumber,
                    BorrowDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(14),
                    Status = BorrowStatus.Pending
                });
                created++;
            }
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(CartKey);

            if (created > 0)
                TempData["Success"] = $"Đã gửi yêu cầu mượn {created} cuốn. Bạn có thể theo dõi trong Lịch sử mượn.";
            if (skipped.Count > 0)
                TempData["Error"] = "Bỏ qua (đã hết sách hoặc đang có yêu cầu/đang mượn): " + string.Join(", ", skipped);
            return RedirectToAction(nameof(LichSu));
        }

        // ---------- Lịch sử mượn của chính tài khoản đang đăng nhập ----------
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> LichSu(string? status, int page = 1)
        {
            var uid = CurrentAccountId;
            var q = _context.BorrowRecords.AsNoTracking().Where(r => r.AccountId == uid);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
            q = q.OrderByDescending(r => r.Id);

            var model = await PaginatedList<BorrowRecord>.CreateAsync(q, page, 8);
            ViewBag.Status = status;
            return View(model);
        }

        // Hủy yêu cầu đang "Chờ duyệt" của chính mình
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> HuyYeuCau(int id)
        {
            var uid = CurrentAccountId;
            var r = await _context.BorrowRecords.FirstOrDefaultAsync(x => x.Id == id && x.AccountId == uid);
            if (r != null && r.Status == BorrowStatus.Pending)
            {
                _context.BorrowRecords.Remove(r);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã hủy yêu cầu mượn";
            }
            return RedirectToAction(nameof(LichSu));
        }

        // ---------- Tài khoản: thông tin cá nhân + đổi mật khẩu ----------
        [HttpGet]
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> TaiKhoan()
        {
            var reader = await _context.Readers.AsNoTracking().FirstOrDefaultAsync(r => r.AccountId == CurrentAccountId);
            return View(new ProfileViewModel
            {
                FullName = reader?.FullName ?? string.Empty,
                Email = reader?.Email ?? string.Empty,
                Phone = reader?.PhoneNumber ?? string.Empty,
                Address = reader?.Address ?? string.Empty
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> TaiKhoan(ProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var uid = CurrentAccountId;
            var reader = await _context.Readers.FirstOrDefaultAsync(r => r.AccountId == uid);
            if (reader == null)
            {
                reader = new Reader { AccountId = uid };
                _context.Readers.Add(reader);
            }
            reader.FullName = model.FullName.Trim();
            reader.Email = model.Email.Trim();
            reader.PhoneNumber = model.Phone.Trim();
            reader.Address = model.Address.Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã lưu thông tin cá nhân";
            return RedirectToAction(nameof(TaiKhoan));
        }

        // Đổi mật khẩu (AJAX, trả JSON)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.DocGia)]
        public async Task<IActionResult> DoiMatKhau(ChangePasswordRequest model)
        {
            var acc = await _context.Accounts.FindAsync(CurrentAccountId);
            if (acc == null) return Forbid();

            if (ModelState.IsValid)
            {
                if (!_accounts.Verify(acc, model.CurrentPassword, out _))
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng");
                var pwErr = AccountService.CheckStrength(model.NewPassword);
                if (pwErr != null) ModelState.AddModelError(nameof(model.NewPassword), pwErr);
            }
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return Json(new { success = false, errors });
            }

            acc.Password = _accounts.Hash(acc, model.NewPassword);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã đổi mật khẩu" });
        }
    }
}
