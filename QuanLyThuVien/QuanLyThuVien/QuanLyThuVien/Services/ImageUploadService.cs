namespace QuanLyThuVien.Services
{
    public class ImageUploadService
    {
        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxBytes = 2 * 1024 * 1024; // 2MB
        private const string Folder = "uploads/books";
        private readonly IWebHostEnvironment _env;

        public ImageUploadService(IWebHostEnvironment env) { _env = env; }

        /// <summary>Lưu ảnh, trả về (đường dẫn, lỗi). Tên file do server tự sinh (chống path traversal / ghi đè).</summary>
        public async Task<(string? Path, string? Error)> SaveAsync(IFormFile file)
        {
            if (file.Length == 0) return (null, "File rỗng");
            if (file.Length > MaxBytes) return (null, "Ảnh tối đa 2MB");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext)) return (null, "Chỉ chấp nhận ảnh .jpg, .png, .gif, .webp");

            // Kiểm tra "magic bytes" để chắc chắn đúng là ảnh, không chỉ tin đuôi file
            var header = new byte[12];
            using (var s = file.OpenReadStream())
            {
                var read = await s.ReadAsync(header, 0, header.Length);
                if (read < 4 || !IsImage(header)) return (null, "File không phải ảnh hợp lệ");
            }

            var dir = Path.Combine(_env.WebRootPath, Folder);
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            using (var fs = new FileStream(Path.Combine(dir, name), FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }
            return ($"/{Folder}/{name}", null);
        }

        public void Delete(string? webPath)
        {
            if (string.IsNullOrEmpty(webPath) || !webPath.StartsWith($"/{Folder}/")) return;
            var full = Path.Combine(_env.WebRootPath, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            // đảm bảo vẫn nằm trong thư mục uploads
            if (full.StartsWith(Path.Combine(_env.WebRootPath, Folder)) && File.Exists(full))
                File.Delete(full);
        }

        private static bool IsImage(byte[] h) =>
            (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) ||                              // JPEG
            (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) ||              // PNG
            (h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) ||              // GIF
            (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 &&
             h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50);              // WEBP
    }
}
