using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace LichCongTacWeb.Models
{
    public class WorkShift
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string ColorCode { get; set; }
    }
}
