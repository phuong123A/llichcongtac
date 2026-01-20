using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LichCongTacWeb.Models
{
    public class ShiftAssignment
    {
        public int Id { get; set; }

        [Required]
        public int LeaderId { get; set; }
        [ForeignKey("LeaderId")]
        public virtual Leader Leader { get; set; }

        [Required]
        public int ShiftId { get; set; }
        [ForeignKey("ShiftId")]
        public virtual WorkShift Shift { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        public string Note { get; set; }
    }
}