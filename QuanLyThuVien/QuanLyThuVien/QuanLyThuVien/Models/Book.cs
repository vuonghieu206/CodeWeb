using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class Book
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sách không được để trống")]
        [StringLength(200, ErrorMessage = "Tên sách tối đa 200 ký tự")]
        [Display(Name = "Tên sách")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tác giả không được để trống")]
        [StringLength(150, ErrorMessage = "Tác giả tối đa 150 ký tự")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Thể loại tối đa 100 ký tự")]
        [Display(Name = "Thể loại")]
        public string? Category { get; set; }

        [Range(0, 10000, ErrorMessage = "Số lượng phải từ 0 đến 10.000")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        // Mô tả (HTML từ editor, luôn được sanitize ở server trước khi lưu)
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        // Đường dẫn ảnh bìa, ví dụ: /uploads/books/abc.jpg
        [StringLength(300)]
        public string? CoverImage { get; set; }
    }
}
