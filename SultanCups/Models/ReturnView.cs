namespace SultanCups.Models
{
    public class ReturnView
    {
        public int return_id { get; set; }

        public int order_id { get; set; }

        public int product_id { get; set; }

        public string product_name { get; set; } = "";

        public int returned_quantity { get; set; }

        public DateTime return_date { get; set; }
    }
}