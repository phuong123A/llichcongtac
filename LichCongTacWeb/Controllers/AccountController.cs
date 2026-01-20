using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using LichCongTacWeb.Data;
using LichCongTacWeb.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LichCongTacWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu!";
                return View();
            }

            // Tìm user và lấy kèm thông tin Role từ bảng liên kết
            var leader = await _context.Leaders
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == username);

            // Kiểm tra thông tin đăng nhập
            if (leader != null && (leader.PasswordHash == password || leader.Password == password))
            {
                // Lấy tên Role từ Database (Dùng RoleName theo SQL của bạn: 'Admin', 'Manager'...)
                string dbRoleName = leader.Role?.RoleName ?? "Employee";

                // Tạo danh sách Claims để xác thực Cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, leader.Id.ToString()),
                    new Claim(ClaimTypes.Name, leader.Username),
                    new Claim("FullName", leader.FullName ?? ""),
                    new Claim(ClaimTypes.Role, dbRoleName),
                    new Claim("RoleId", leader.RoleId.ToString()) // Lưu ID vào Claim
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                // Đăng nhập hệ thống (Cookie)
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) // Giữ phiên đăng nhập 12 tiếng
                    });

                // --- LƯU SESSION ĐỂ LAYOUT VÀ ADMINCONTROLLER DÙNG (CỰC KỲ QUAN TRỌNG) ---
                // Xóa session cũ nếu có để tránh rác dữ liệu
                HttpContext.Session.Clear();

                HttpContext.Session.SetInt32("LeaderId", leader.Id);
                HttpContext.Session.SetInt32("RoleId", leader.RoleId); // Đây là số 4 cho Thư ký
                HttpContext.Session.SetString("Role", dbRoleName);     // Thường là 'Manager' theo SQL của bạn
                HttpContext.Session.SetString("FullName", leader.FullName ?? "");

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác!";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Đăng xuất Cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Xóa sạch Session
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();
    }
}