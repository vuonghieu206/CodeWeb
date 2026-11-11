using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Controllers
{
    /// <summary>Lớp cha của mọi trang quản trị: chỉ Admin / Thủ thư mới vào được (độc giả bị chặn 403).</summary>
    [Authorize(Roles = AppRoles.Staff)]
    public abstract class StaffController : Controller
    {
    }
}
