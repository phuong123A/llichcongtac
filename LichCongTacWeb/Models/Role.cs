using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LichCongTacWeb.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RoleName { get; set; } = string.Empty;

        // Một Role có thể có nhiều Leader (Quan hệ 1 - Nhiều)
        public virtual ICollection<Leader> Leaders { get; set; } = new List<Leader>();
    }
}