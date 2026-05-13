using System.ComponentModel.DataAnnotations;

namespace SultanCups.Models
{
    public class ArchiveCycle
    {
        [Key]
        public int archive_id { get; set; }

        public DateTime archive_date { get; set; }

        public int? archived_by { get; set; }

        public string? notes { get; set; }

        // =====================================
        // البطاقات الرئيسية
        // =====================================

        public decimal total_cash_balance { get; set; }

        public decimal real_financial_balance { get; set; }

        public decimal total_debts { get; set; }

        public decimal total_in { get; set; }

        public decimal total_out { get; set; }

        public decimal total_sales_collected { get; set; }

        public decimal total_purchases { get; set; }

        public decimal total_loans { get; set; }

        public decimal salaries_remaining { get; set; }

        public decimal commissions_unpaid { get; set; }

        public int employees_count { get; set; }

        public int total_production_quantity { get; set; }

        public int total_returns_count { get; set; }

        public int total_returns_boxes { get; set; }

        // =====================================
        // أفضل العناصر
        // =====================================

        public string? best_marketer_name { get; set; }

        public string? best_customer_name { get; set; }

        public string? best_supplier_name { get; set; }

        // =====================================
        // المنتجات
        // =====================================

        public string? most_sold_product_name { get; set; }

        public string? most_produced_product_name { get; set; }
    }
}