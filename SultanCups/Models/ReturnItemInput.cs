namespace SultanCups.Models
{
    public class ReturnItemInput
    {
        public int product_id { get; set; }

        public string product_name { get; set; } = "";

        public int sold_quantity { get; set; }

        public int return_quantity { get; set; }

        public decimal unit_price { get; set; }

        public decimal commission_per_box { get; set; }
    }
}