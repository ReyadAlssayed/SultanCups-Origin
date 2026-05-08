using System.ComponentModel.DataAnnotations;

namespace SultanCups.Models
{
    public class Return
    {
        [Key]
        public int return_id { get; set; }

        public int order_id { get; set; }

        public int product_id { get; set; }

        public DateTime return_date { get; set; }

        public int returned_quantity { get; set; }
    }
}