using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public static class BorrowStatus
    {
        public const string Pending = "Chờ duyệt";   // Khách gửi yêu cầu
        public const string Borrowing = "Đang mượn";
        public const string Returned = "Đã trả";
        public const string Rejected = "Từ chối";
        public static readonly string[] All = { Pending, Borrowing, Returned, Rejected };
    }

    public class BorrowRecord
    {
        [Key]
        public int Id { get; set; }

        // Tài khoản độc giả gửi yêu cầu (null nếu thủ thư tạo phiếu tại quầy / dữ liệu cũ)
        public int? AccountId { get; set; }

        // Liên kết mềm tới Books.Id (dữ liệu cũ có thể null)
        public int? BookId { get; set; }

        [Required(ErrorMessage = "Tên sách không được để trống")]
        [StringLength(200)]
        [Display(Name = "Tên sách")]
        public string BookName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên người mượn không được để trống")]
        [StringLength(100)]
        [Display(Name = "Người mượn")]
        public string ReaderName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "SĐT liên hệ")]
        public string? ContactPhone { get; set; }

        [Display(Name = "Ngày mượn")]
        public DateTime BorrowDate { get; set; } = DateTime.Now;

        [Display(Name = "Hạn trả")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Ngày trả")]
        public DateTime? ReturnDate { get; set; }

        [StringLength(30)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = BorrowStatus.Borrowing;
    }
}
