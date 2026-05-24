using System.ComponentModel.DataAnnotations;

namespace SultanCups.Models
{
    public class QuickNote
    {
        [Key]
        public int note_id { get; set; }

        public string note_text { get; set; } = "";

        public DateTime created_at { get; set; }
    }
}