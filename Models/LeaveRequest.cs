using System.ComponentModel.DataAnnotations;

namespace LichCongTacWeb.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required]
        public int LeaderId { get; set; }
        public Leader? Leader { get; set; } 

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public string? Reason { get; set; }

        // 0: Chờ duyệt, 1: Đã duyệt, 2: Từ chối
        public int Status { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}