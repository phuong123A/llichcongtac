using System.ComponentModel.DataAnnotations;

namespace LichCongTacWeb.Models
{
    public class Location
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên địa điểm không được trống")]
        public string Name { get; set; }
    }
}