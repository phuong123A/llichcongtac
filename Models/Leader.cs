using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace LichCongTacWeb.Models
{
    [Table("leaders")] // Đảm bảo khớp với tên bảng trong SQL của bạn
    public class Leader
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        // Cột lưu mật khẩu đã mã hóa (hoặc dùng để so khớp)
        [StringLength(255)]
        public string? PasswordHash { get; set; }

        /// <summary>
        /// Dựa theo file SQL của bạn, bảng leaders CÓ cột Password.
        /// Việc giữ thuộc tính này giúp AdminController gán dữ liệu không bị lỗi gạch đỏ.
        /// </summary>
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Position { get; set; }

        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn quyền hệ thống")]
        public int RoleId { get; set; }

        public int? DisplayOrder { get; set; }

        // --- Các mối quan hệ (Navigation Properties) ---

        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

        // Thêm nếu bạn có tính năng quản lý đơn nghỉ phép
        public virtual ICollection<LeaveRequest>? LeaveRequests { get; set; }
    }
}