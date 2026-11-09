using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyThuVien.Models
{
    public class Penalty
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên độc giả không được để trống")]
        [StringLength(100)]
        public string ReaderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lý do phạt không được để trống")]
        [StringLength(300)]
        public string Reason { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 100000000, ErrorMessage = "Số tiền phạt không hợp lệ")]
        public decimal Amount { get; set; }

        public bool IsPaid { get; set; } // true: Đã nộp, false: Chưa nộp
    }
}
