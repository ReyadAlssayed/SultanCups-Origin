namespace SultanCups.Models
{
    public class OrderView
    {
        public int order_id { get; set; }

        public string person_type { get; set; } = "";
        public string person_name { get; set; } = "";

        public int items_count { get; set; }

        // 🔥 أضف هذا هنا
        public int total_quantity { get; set; }

        public int person_id { get; set; }   // 🔥 مهم

        public bool is_special { get; set; }   // 🔥 جديد

        public bool is_cancelled { get; set; }
        public decimal total { get; set; }
        public decimal discount_total { get; set; }
        public decimal net_total { get; set; }

        public decimal commission_total { get; set; }

        public bool pay_commission_now { get; set; }

        public decimal paid_amount { get; set; }

        public decimal profit { get; set; }

        public DateTime order_date { get; set; }

        public DateTime created_at { get; set; }
    }
}