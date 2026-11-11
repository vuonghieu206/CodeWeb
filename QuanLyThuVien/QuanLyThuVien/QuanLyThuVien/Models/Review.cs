using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    // Đánh giá của độc giả cho một cuốn sách: số sao + bình luận + thời gian
    public class Review
    {
        [Key]
        public int Id { get; set; }

        public int BookId { get; set; }

        // Tài khoản đã đánh giá (liên kết mềm, không khóa ngoại để xóa tài khoản không mất đánh giá)
        public int AccountId { get; set; }

        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;   // tên hiển thị tại thời điểm đánh giá

        [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Bình luận tối đa 1000 ký tự")]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
