using Microsoft.AspNetCore.Mvc;

namespace LichCongTacWeb.Models
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Leader> Leaders { get; set; }
    }
}