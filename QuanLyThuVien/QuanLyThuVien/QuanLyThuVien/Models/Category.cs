using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    // Danh mục (thể loại) sách. Sách chỉ được chọn thể loại có trong bảng này.
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }
    }
}
