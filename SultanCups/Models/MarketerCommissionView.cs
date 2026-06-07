namespace SultanCups.Models

{
    public class MarketerCommissionView
    {
        public int order_id { get; set; }

        public string marketer_name { get; set; } = "";

        public decimal order_total { get; set; }

        public decimal commission_total { get; set; }

        public string commission_status { get; set; } = "";

        public DateTime order_date { get; set; }

        public DateTime created_at { get; set; }

    }
}
