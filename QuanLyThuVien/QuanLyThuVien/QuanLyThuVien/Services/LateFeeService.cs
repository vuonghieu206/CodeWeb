using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Services
{
    /// <summary>
    /// Tính tiền phạt trả sách quá hạn: FeePerDay đồng / ngày.
    /// Mỗi phiếu quá hạn có đúng 1 khoản phạt, nhận biết bằng mã "[PM#id]" ở cuối cột Lý do
    /// (không cần thêm cột/migration mới). Khoản phạt đã nộp thì không bị cập nhật nữa.
    /// </summary>
    public class LateFeeService
    {
        public const decimal FeePerDay = 2000m;
        private readonly LibraryDbContext _db;

        public LateFeeService(LibraryDbContext db) { _db = db; }

        public static string Marker(int borrowId) => $"[PM#{borrowId}]";

        public static int OverdueDays(BorrowRecord r, DateTime now) =>
            r.DueDate.HasValue && now.Date > r.DueDate.Value.Date ? (now.Date - r.DueDate.Value.Date).Days : 0;

        /// <summary>Tạo hoặc cập nhật khoản phạt của phiếu (chưa SaveChanges).</summary>
        public async Task UpsertAsync(BorrowRecord r, int days)
        {
            if (days <= 0) return;
            var marker = Marker(r.Id);
            var reason = $"Trả sách \"{r.BookName}\" quá hạn {days} ngày {marker}";
            var amount = days * FeePerDay;

            var penalty = await _db.Penalties.FirstOrDefaultAsync(p => p.Reason.Contains(marker));
            if (penalty == null)
            {
                _db.Penalties.Add(new Penalty { ReaderName = r.ReaderName, Reason = reason, Amount = amount, IsPaid = false });
            }
            else if (!penalty.IsPaid)
            {
                penalty.Reason = reason;
                penalty.Amount = amount;
            }
        }

        /// <summary>Quét mọi phiếu đang mượn đã quá hạn, tạo/cập nhật phạt. Trả về số dòng DB thay đổi.</summary>
        public async Task<int> SyncOverdueAsync()
        {
            var now = DateTime.Now;
            var overdue = await _db.BorrowRecords
                .Where(r => r.Status == BorrowStatus.Borrowing && r.DueDate.HasValue && r.DueDate.Value < now.Date)
                .ToListAsync();
            foreach (var r in overdue)
                await UpsertAsync(r, OverdueDays(r, now));
            return await _db.SaveChangesAsync();
        }
    }
}
