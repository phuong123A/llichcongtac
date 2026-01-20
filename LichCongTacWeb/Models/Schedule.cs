using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LichCongTacWeb.Models
{
    public class Schedule
    {
        public int Id { get; set; }
        public int LeaderId { get; set; }
        public DateTime WorkDate { get; set; }
        public string? TimeSlot { get; set; }
        public string? Content { get; set; }
        public string? Location { get; set; }
        public int? RoomId { get; set; }
        public string? Category { get; set; }
        public int? DepartmentId { get; set; }
        public int Scope { get; set; }
        public string? AssigneeIds { get; set; }
        public string? Status { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int? CreatedBy { get; set; }
        public string? Participants { get; set; }
        public string? AttachmentPath { get; set; }

        [ForeignKey("LeaderId")]
        public virtual Leader? Leader { get; set; }
    }
}