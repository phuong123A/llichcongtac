using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using LichCongTacWeb.Data;
using LichCongTacWeb.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LichCongTacWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int weekOffset = 0, int? leaderId = null, int? deptId = null)
        {
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = today.AddDays(-diff).AddDays(weekOffset * 7);

            ViewBag.StartOfWeek = startOfWeek;
            ViewBag.WeekOffset = weekOffset;
            ViewBag.Today = today;

            int? loggedInLeaderId = HttpContext.Session.GetInt32("LeaderId");
            ViewBag.IsLoggedIn = loggedInLeaderId.HasValue;

            if (loggedInLeaderId.HasValue)
            {
                var currentLeader = await _context.Leaders
                    .Include(l => l.Department)
                    .FirstOrDefaultAsync(l => l.Id == loggedInLeaderId.Value);

                if (currentLeader != null)
                {
                    ViewBag.Departments = new List<Department> { currentLeader.Department };
                    ViewBag.Leaders = new List<Leader> { currentLeader };
                    ViewBag.SelectedLeaderId = currentLeader.Id;
                    ViewBag.SelectedDeptId = currentLeader.DepartmentId;
                }
            }
            else
            {
                ViewBag.Departments = await _context.Departments.ToListAsync();

                var leaderDropdownQuery = _context.Leaders.AsQueryable();
                if (deptId.HasValue)
                    leaderDropdownQuery = leaderDropdownQuery.Where(l => l.DepartmentId == deptId);

                ViewBag.Leaders = await leaderDropdownQuery.OrderBy(l => l.DisplayOrder).ToListAsync();
                ViewBag.SelectedLeaderId = leaderId;
                ViewBag.SelectedDeptId = deptId;
            }

            var query = _context.Leaders.Include(l => l.Schedules).AsQueryable();

            if (loggedInLeaderId.HasValue)
            {
                query = query.Where(l => l.Id == loggedInLeaderId.Value);
            }
            else
            {
                if (deptId.HasValue) query = query.Where(l => l.DepartmentId == deptId);
                if (leaderId.HasValue) query = query.Where(l => l.Id == leaderId);
            }

            var data = await query.OrderBy(l => l.DisplayOrder).ToListAsync();
            return View(data);
        }

        // --- PHẦN THÊM MỚI ĐỂ XỬ LÝ TẢI FILE ---
        [HttpPost]
        public async Task<IActionResult> UploadAttachment(int scheduleId, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                try
                {
                    // 1. Định nghĩa đường dẫn lưu file: wwwroot/uploads
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    // Tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // 2. Tạo tên file duy nhất bằng Guid để tránh trùng lặp
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // 3. Copy file vào thư mục trên server
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    // 4. Cập nhật cơ sở dữ liệu
                    var schedule = await _context.Schedules.FindAsync(scheduleId);
                    if (schedule != null)
                    {
                        schedule.AttachmentPath = "/uploads/" + uniqueFileName;
                        _context.Update(schedule);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Có thể thêm thông báo lỗi vào TempData nếu cần
                    TempData["Error"] = "Lỗi khi tải file: " + ex.Message;
                }
            }

            // Quay lại trang Index với các tham số cũ (nếu có)
            return RedirectToAction(nameof(Index));
        }
    }
}