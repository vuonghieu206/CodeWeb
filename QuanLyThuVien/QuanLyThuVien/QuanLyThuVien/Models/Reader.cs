using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class Reader
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên độc giả không được để trống")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(150)]
        public string? Email { get; set; }

        // Có giá trị khi độc giả này được tạo từ việc đăng ký tài khoản trên website
        public int? AccountId { get; set; }

        [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }
    }
}
