using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SultanCups.Models
{
    public class OrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // 🔥 مهم
        public int order_item_id { get; set; }

        public int order_id { get; set; }

        public int product_id { get; set; }

        [NotMapped]
        public string? product_name { get; set; }

        public int quantity { get; set; }

        public decimal unit_price { get; set; }

        public decimal production_cost { get; set; }

        [NotMapped]
        public decimal original_reference_value { get; set; }

        [NotMapped]
        public decimal total => quantity * unit_price;

        public Order? Order { get; set; }

        [NotMapped]
        public bool IsNew { get; set; } = false;

        [NotMapped]
        public bool IsRemoving { get; set; } = false;
    }
}