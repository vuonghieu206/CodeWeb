using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string ThuThu = "Thủ thư";
        public const string Staff = Admin + "," + ThuThu; // dùng cho [Authorize(Roles = ...)]
    }

    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Chỉ dùng chữ không dấu, số, dấu . và _")]
        public string Username { get; set; } = string.Empty;

        // Lưu dạng HASH (PBKDF2), không bao giờ lưu mật khẩu thô
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [StringLength(300)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn phân quyền")]
        public string Role { get; set; } = AppRoles.ThuThu;

        public bool IsActive { get; set; } = true;
    }
}
