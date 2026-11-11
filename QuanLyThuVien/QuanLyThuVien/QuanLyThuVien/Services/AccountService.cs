using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Services
{
    /// <summary>Băm / kiểm tra mật khẩu bằng PBKDF2 (PasswordHasher của ASP.NET Core).</summary>
    public class AccountService
    {
        private readonly PasswordHasher<Account> _hasher = new();

        /// <summary>Chính sách mật khẩu: tối thiểu 8 ký tự, có cả chữ và số. Trả về lỗi hoặc null nếu hợp lệ.</summary>
        public static string? CheckStrength(string? pw)
        {
            if (string.IsNullOrEmpty(pw) || pw.Length < 8) return "Mật khẩu tối thiểu 8 ký tự";
            if (!pw.Any(char.IsLetter) || !pw.Any(char.IsDigit)) return "Mật khẩu phải có cả chữ và số";
            return null;
        }

        public string Hash(Account acc, string plain) => _hasher.HashPassword(acc, plain);

        // Hash của PasswordHasher (format V3) luôn bắt đầu bằng "AQAAAA" khi mã hóa base64
        private static bool LooksHashed(string stored) => stored.StartsWith("AQAAAA", StringComparison.Ordinal);

        /// <summary>
        /// Trả về true nếu đúng. needsRehash=true khi DB còn mật khẩu thô (dữ liệu cũ) hoặc hash cũ
        /// -> controller sẽ băm lại và lưu.
        /// </summary>
        public bool Verify(Account acc, string plain, out bool needsRehash)
        {
            needsRehash = false;

            if (!LooksHashed(acc.Password))
            {
                // Dữ liệu cũ lưu mật khẩu thô: so sánh thời gian cố định rồi băm lại ngay sau khi đăng nhập
                var a = Encoding.UTF8.GetBytes(acc.Password);
                var b = Encoding.UTF8.GetBytes(plain);
                if (a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b))
                {
                    needsRehash = true;
                    return true;
                }
                return false;
            }

            try
            {
                var r = _hasher.VerifyHashedPassword(acc, acc.Password, plain);
                if (r == PasswordVerificationResult.Success) return true;
                if (r == PasswordVerificationResult.SuccessRehashNeeded) { needsRehash = true; return true; }
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
