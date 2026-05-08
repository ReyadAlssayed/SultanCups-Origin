namespace SultanCups.Models
{
    public class StatsView
    {
        // =========================
        // البطاقات العلوية
        // =========================

        public decimal monthly_profit { get; set; }

        public int monthly_boxes_sold { get; set; }

        public int monthly_orders_count { get; set; }

        public decimal total_debts { get; set; }

        public decimal total_cash_balance { get; set; }

        // =========================
        // الرسم البياني
        // =========================

        public List<ProfitChartItem> daily_profit_chart { get; set; } = new();

        public List<ProfitChartItem> weekly_profit_chart { get; set; } = new();

        public List<ProfitChartItem> monthly_profit_chart { get; set; } = new();

        // =========================
        // توزيع المبيعات
        // =========================

        public List<ProductSalesDistribution> sales_distribution { get; set; } = new();

        // =========================
        // الخزنات
        // =========================

        public List<CashBoxStatsItem> cash_boxes { get; set; } = new();

        // =========================
        // ديون الزبائن
        // =========================

        public List<CustomerDebtItem> customer_debts { get; set; } = new();

        // =========================
        // ديون المسوقين
        // =========================

        public List<MarketerDebtItem> marketer_debts { get; set; } = new();

        // =========================
        // المنتجات الناقصة
        // =========================

        public List<LowStockItem> low_stock_products { get; set; } = new();

        // =========================
        // أفضل المسوقين
        // =========================

        public List<TopMarketerItem> top_marketers { get; set; } = new();
    }

    // =========================================
    // الرسم البياني
    // =========================================

    public class ProfitChartItem
    {
        public string label { get; set; } = "";

        public decimal value { get; set; }
    }

    // =========================================
    // توزيع المنتجات
    // =========================================

    public class ProductSalesDistribution
    {
        public string product_name { get; set; } = "";

        public int quantity { get; set; }

        public decimal percentage { get; set; }
    }

    // =========================================
    // الخزنات
    // =========================================

    public class CashBoxStatsItem
    {
        public int cash_box_id { get; set; }

        public string cash_box_name { get; set; } = "";

        public decimal current_balance { get; set; }

        public decimal monthly_in { get; set; }

        public decimal monthly_out { get; set; }
    }

    // =========================================
    // ديون الزبائن
    // =========================================

    public class CustomerDebtItem
    {
        public int customer_id { get; set; }

        public string customer_name { get; set; } = "";

        public decimal debt_amount { get; set; }
    }

    // =========================================
    // ديون المسوقين
    // =========================================

    public class MarketerDebtItem
    {
        public int marketer_id { get; set; }

        public string marketer_name { get; set; } = "";

        public decimal debt_amount { get; set; }
    }

    // =========================================
    // المنتجات الناقصة
    // =========================================

    public class LowStockItem
    {
        public int product_id { get; set; }

        public string product_name { get; set; } = "";

        public int current_quantity { get; set; }
    }

    // =========================================
    // أفضل المسوقين
    // =========================================

    public class TopMarketerItem
    {
        public int marketer_id { get; set; }

        public string marketer_name { get; set; } = "";

        public int orders_count { get; set; }

        public decimal total_sales { get; set; }

        public decimal total_commissions { get; set; }
    }
}