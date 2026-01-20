using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using LichCongTacWeb.Data;
using LichCongTacWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;

namespace LichCongTacWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        #region 1. HELPERS (HÀM TRỢ GIÚP PHÂN QUYỀN)
        private string CurrentRole => HttpContext.Session.GetString("Role") ?? "Employee";
        private int CurrentRoleId => HttpContext.Session.GetInt32("RoleId") ?? 0;
        private int? CurrentLeaderId => HttpContext.Session.GetInt32("LeaderId");

        private bool IsAdmin => CurrentRoleId == 1 || CurrentRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsSecretary => CurrentRoleId == 4 ||
                                    CurrentRole.Contains("Thư ký") ||
                                    CurrentRole.Equals("Secretary", StringComparison.OrdinalIgnoreCase);

        private bool HasEditPermission => IsAdmin || IsSecretary;

        private IActionResult UnauthorizedResponse() => RedirectToAction("Index", "Home");
        private IActionResult AjaxUnauthorized() => Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });

        private DateTime GetStartOfWeek(int weekOffset)
        {
            var today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            return today.AddDays(-1 * diff).AddDays(weekOffset * 7);
        }
        #endregion

        #region 2. QUẢN LÝ LỊCH CÔNG TÁC
        public async Task<IActionResult> ManageSchedules(int weekOffset = 0, int? deptId = null)
        {
            if (!HasEditPermission) return UnauthorizedResponse();

            var startOfWeek = GetStartOfWeek(weekOffset);
            var endOfWeek = startOfWeek.AddDays(6);

            var departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();

            var schedulesQuery = _context.Schedules.AsNoTracking()
                                 .Where(s => s.WorkDate >= startOfWeek && s.WorkDate <= endOfWeek);

            var leadersQuery = _context.Leaders.AsNoTracking();

            if (deptId.HasValue && deptId > 0)
            {
                // Khi lọc phòng ban, CHỈ lọc cán bộ hiển thị trên dòng (Model)
                leadersQuery = leadersQuery.Where(l => l.DepartmentId == deptId);

                // Lịch hiển thị phải bao gồm lịch của đơn vị đó HOẶC lịch toàn cơ quan (Scope = 2)
                schedulesQuery = schedulesQuery.Where(s => s.DepartmentId == deptId || s.Scope == 2);
            }

            var leaders = await leadersQuery.OrderBy(l => l.DisplayOrder).ToListAsync();
            var schedules = await schedulesQuery.ToListAsync();

            ViewBag.DaysInWeek = Enumerable.Range(0, 7).Select(i => startOfWeek.AddDays(i)).ToList();
            ViewBag.Departments = departments;
            ViewBag.CurrentDeptId = deptId ?? 0;
            ViewBag.WeekOffset = weekOffset;
            ViewBag.Schedules = schedules;

            // SỬA TẠI ĐÂY: Luôn lấy toàn bộ danh sách cán bộ để View map ID sang Tên
            ViewBag.AllLeaders = await _context.Leaders.AsNoTracking()
                .Select(l => new Leader { Id = l.Id, FullName = l.FullName, Position = l.Position, DepartmentId = l.DepartmentId })
                .ToListAsync();

            ViewBag.PendingLeaves = await _context.LeaveRequests.CountAsync(r => r.Status == 0);

            return View(leaders);
        }

        [HttpGet]
        public async Task<IActionResult> CreateSchedule(int? id, int? leaderId, string date)
        {
            if (!HasEditPermission) return UnauthorizedResponse();

            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            // Lấy toàn bộ cán bộ để chọn Thành phần phối hợp
            ViewBag.AllLeaders = await _context.Leaders.AsNoTracking().OrderBy(l => l.FullName).ToListAsync();

            Schedule model;
            if (id.HasValue && id > 0)
            {
                model = await _context.Schedules.FindAsync(id.Value);
                if (model == null) return NotFound();
            }
            else
            {
                model = new Schedule();
                if (leaderId.HasValue)
                {
                    model.LeaderId = leaderId.Value;
                    var leader = await _context.Leaders.FindAsync(leaderId.Value);
                    if (leader != null) model.DepartmentId = leader.DepartmentId;
                }

                if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out DateTime workDate))
                    model.WorkDate = workDate;
                else
                    model.WorkDate = DateTime.Today;
            }

            return View("CreateSchedule", model);
        }

        [HttpGet]
        public async Task<IActionResult> EditSchedule(int id)
        {
            return await CreateSchedule(id, null, null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(Schedule schedule, List<IFormFile>? AttachmentFiles)
        {
            if (!HasEditPermission) return UnauthorizedResponse();

            try
            {
                // Xử lý file đính kèm
                if (AttachmentFiles != null && AttachmentFiles.Count > 0)
                {
                    List<string> savedFilePaths = new List<string>();
                    string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "attachments");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    foreach (var file in AttachmentFiles)
                    {
                        if (file.Length > 0)
                        {
                            string fileName = $"Doc_{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(file.FileName)}";
                            string filePath = Path.Combine(uploadPath, fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            savedFilePaths.Add("/attachments/" + fileName);
                        }
                    }

                    string newPaths = string.Join(",", savedFilePaths);
                    if (schedule.Id > 0)
                    {
                        var existingShed = await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schedule.Id);
                        schedule.AttachmentPath = string.IsNullOrEmpty(existingShed?.AttachmentPath)
                            ? newPaths
                            : existingShed.AttachmentPath + "," + newPaths;
                    }
                    else
                    {
                        schedule.AttachmentPath = newPaths;
                    }
                }
                else if (schedule.Id > 0)
                {
                    var existingShed = await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schedule.Id);
                    if (existingShed != null) schedule.AttachmentPath = existingShed.AttachmentPath;
                }

                schedule.Status = schedule.Status ?? "Pending";

                if (schedule.Id == 0)
                {
                    schedule.CreatedBy = CurrentLeaderId;
                    _context.Schedules.Add(schedule);
                }
                else
                {
                    // Đảm bảo không làm mất thông tin người tạo khi update
                    var existing = await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schedule.Id);
                    if (existing != null) schedule.CreatedBy = existing.CreatedBy;

                    _context.Schedules.Update(schedule);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("ManageSchedules");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi lưu: " + ex.Message);
                ViewBag.Departments = await _context.Departments.AsNoTracking().ToListAsync();
                ViewBag.AllLeaders = await _context.Leaders.AsNoTracking().ToListAsync();
                return View("CreateSchedule", schedule);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSchedule(Schedule schedule, List<IFormFile>? AttachmentFiles)
        {
            if (!HasEditPermission && schedule.LeaderId != CurrentLeaderId)
                return AjaxUnauthorized();

            try
            {
                string newUploadedPaths = "";
                if (AttachmentFiles != null && AttachmentFiles.Count > 0)
                {
                    List<string> savedFilePaths = new List<string>();
                    string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "attachments");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    foreach (var file in AttachmentFiles)
                    {
                        if (file.Length > 0)
                        {
                            string fileName = $"Doc_{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(file.FileName)}";
                            string filePath = Path.Combine(uploadPath, fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                            savedFilePaths.Add("/attachments/" + fileName);
                        }
                    }
                    newUploadedPaths = string.Join(",", savedFilePaths);
                }

                if (schedule.Id == 0)
                {
                    schedule.AttachmentPath = newUploadedPaths;
                    schedule.Status = "Pending";
                    schedule.CreatedBy = CurrentLeaderId;
                    _context.Schedules.Add(schedule);
                }
                else
                {
                    var existing = await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == schedule.Id);
                    if (existing != null)
                    {
                        schedule.AttachmentPath = string.IsNullOrEmpty(newUploadedPaths)
                            ? existing.AttachmentPath
                            : (string.IsNullOrEmpty(existing.AttachmentPath) ? newUploadedPaths : existing.AttachmentPath + "," + newUploadedPaths);
                        schedule.CreatedBy = existing.CreatedBy;
                        schedule.Status = existing.Status;
                    }
                    _context.Entry(schedule).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }
        #endregion

        #region 3. DANH MỤC CÁN BỘ
        public async Task<IActionResult> Leaders()
        {
            if (!IsAdmin) return UnauthorizedResponse();
            var leaders = await _context.Leaders.Include(l => l.Department).OrderBy(l => l.DisplayOrder).ToListAsync();
            ViewBag.Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            return View(leaders);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLeader(Leader leader, string Password)
        {
            if (!IsAdmin) return AjaxUnauthorized();
            try
            {
                if (leader.Id == 0)
                {
                    if (await _context.Leaders.AnyAsync(l => l.Username == leader.Username))
                        return Json(new { success = false, message = "Tên đăng nhập đã tồn tại!" });
                    string clearPass = string.IsNullOrEmpty(Password) ? "123456" : Password;
                    leader.PasswordHash = clearPass; leader.Password = clearPass;
                    _context.Leaders.Add(leader);
                }
                else
                {
                    var existing = await _context.Leaders.FindAsync(leader.Id);
                    if (existing == null) return Json(new { success = false, message = "Không tìm thấy!" });
                    existing.FullName = leader.FullName; existing.Position = leader.Position;
                    existing.DepartmentId = (leader.DepartmentId == 0) ? null : leader.DepartmentId;
                    existing.RoleId = leader.RoleId; existing.DisplayOrder = leader.DisplayOrder;
                    if (!string.IsNullOrEmpty(Password)) { existing.PasswordHash = Password; existing.Password = Password; }
                    _context.Update(existing);
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteLeader(int id)
        {
            if (!IsAdmin) return Json(new { success = false, message = "Bạn không có quyền quản trị!" });
            try
            {
                var leader = await _context.Leaders.FindAsync(id);
                if (leader == null) return Json(new { success = false, message = "Không tìm thấy cán bộ." });

                bool hasSchedules = await _context.Schedules.AnyAsync(s => s.LeaderId == id);
                if (hasSchedules)
                {
                    return Json(new { success = false, message = "Không thể xóa vì cán bộ này đang có dữ liệu lịch công tác. Hãy xóa lịch trước!" });
                }

                _context.Leaders.Remove(leader);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message }); }
        }
        #endregion

        #region 4. QUẢN LÝ NGHỈ PHÉP
        public async Task<IActionResult> LeaveRequests()
        {
            if (string.IsNullOrEmpty(CurrentRole)) return UnauthorizedResponse();
            var query = _context.LeaveRequests.Include(r => r.Leader).AsQueryable();
            if (!HasEditPermission) query = query.Where(r => r.LeaderId == CurrentLeaderId);
            return View(await query.OrderByDescending(r => r.CreatedAt).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int requestId, bool isApproved)
        {
            if (!HasEditPermission) return AjaxUnauthorized();
            var request = await _context.LeaveRequests.Include(r => r.Leader).FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null) return Json(new { success = false });
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (isApproved)
                {
                    request.Status = 1;
                    for (var date = request.StartDate.Date; date <= request.EndDate.Date; date = date.AddDays(1))
                    {
                        var exist = await _context.Schedules.FirstOrDefaultAsync(s => s.LeaderId == request.LeaderId && s.WorkDate.Date == date);
                        if (exist != null) { exist.Content = "NGHỈ PHÉP (Đã duyệt)"; exist.Status = "Done"; exist.IsApproved = true; _context.Update(exist); }
                        else { _context.Schedules.Add(new Schedule { LeaderId = request.LeaderId, DepartmentId = request.Leader?.DepartmentId, WorkDate = date, Content = "NGHỈ PHÉP (Đã duyệt)", TimeSlot = "Cả ngày", Status = "Done", IsApproved = true }); }
                    }
                }
                else request.Status = 2;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { await transaction.RollbackAsync(); return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SendLeaveRequest(DateTime startDate, DateTime endDate, string reason)
        {
            if (CurrentLeaderId == null) return Json(new { success = false, message = "Hết phiên!" });
            try
            {
                var request = new LeaveRequest { LeaderId = CurrentLeaderId.Value, StartDate = startDate, EndDate = endDate, Reason = reason, Status = 0, CreatedAt = DateTime.Now };
                _context.LeaveRequests.Add(request);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        #endregion

        #region 5. FILE UPLOAD
        [HttpPost]
        public async Task<IActionResult> UploadAttachment(int scheduleId, IFormFile file)
        {
            if (!HasEditPermission) return AjaxUnauthorized();
            var schedule = await _context.Schedules.FindAsync(scheduleId);
            if (schedule == null || file == null) return Json(new { success = false });
            try
            {
                string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "attachments");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);
                string fileName = $"Doc_{scheduleId}_{Guid.NewGuid().ToString().Substring(0, 8)}{Path.GetExtension(file.FileName)}";
                string filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                schedule.AttachmentPath = "/attachments/" + fileName;
                _context.Update(schedule);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        #endregion

        #region 6. DELETE SCHEDULE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            if (!HasEditPermission) return AjaxUnauthorized();
            try
            {
                var schedule = await _context.Schedules.FindAsync(id);
                if (schedule == null) return Json(new { success = false, message = "Không tìm thấy!" });
                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        #endregion

        #region 7. QUẢN LÝ ĐƠN VỊ
        [HttpPost]
        public async Task<IActionResult> SaveDepartment(int id, string name)
        {
            if (!IsAdmin) return AjaxUnauthorized();
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Trống tên!" });
            try
            {
                if (id == 0) _context.Departments.Add(new Department { Name = name.Trim() });
                else
                {
                    var existing = await _context.Departments.FindAsync(id);
                    if (existing == null) return Json(new { success = false });
                    existing.Name = name.Trim(); _context.Departments.Update(existing);
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            if (!IsAdmin) return AjaxUnauthorized();
            try
            {
                var dept = await _context.Departments.FindAsync(id);
                if (dept == null) return Json(new { success = false });
                if (await _context.Leaders.AnyAsync(l => l.DepartmentId == id))
                    return Json(new { success = false, message = "Đơn vị đang có cán bộ!" });
                _context.Departments.Remove(dept);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
        #endregion
    }
}
