namespace SultanCups.Models
{
    public class DebtView
    {
        public int order_id { get; set; }

        public string person_name { get; set; } = "";

        public string person_type { get; set; } = ""; // 🔥 مهم

        public DateTime order_date { get; set; }

        public DateTime created_at { get; set; }

        public decimal net_total { get; set; }

        public decimal paid_amount { get; set; }

        public decimal remaining { get; set; }

        public string status { get; set; } = "";
    }
}