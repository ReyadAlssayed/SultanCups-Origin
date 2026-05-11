namespace SultanCups.Models
{
    public class StatsView
    {
        // =====================================
        // البطاقات الرئيسية
        // =====================================

        // إجمالي السيولة الحقيقية
        public decimal total_cash_balance { get; set; }

        // إجمالي أرباح الفواتير
        public decimal total_orders_profit { get; set; }

        // إجمالي خسائر الفواتير
        public decimal total_orders_loss { get; set; }

        // إجمالي الديون الحالية
        public decimal total_debts { get; set; }

        // الداخل هذا الشهر
        public decimal monthly_in { get; set; }

        // الخارج هذا الشهر
        public decimal monthly_out { get; set; }

        // المبيعات المحصلة هذا الشهر
        public decimal monthly_sales_collected { get; set; }

        // المشتريات هذا الشهر
        public decimal monthly_purchases { get; set; }

        // السلف هذا الشهر
        public decimal monthly_loans { get; set; }

        // الرواتب المدفوعة
        public decimal salaries_paid { get; set; }

        // الرواتب المستحقة
        public decimal salaries_remaining { get; set; }

        // العمولات المدفوعة
        public decimal commissions_paid { get; set; }

        // العمولات غير المدفوعة
        public decimal commissions_unpaid { get; set; }

        // عدد الموظفين
        public int employees_count { get; set; }

        // إجمالي الإنتاج هذا الشهر
        public int monthly_production_quantity { get; set; }

        // عدد المرجوعات هذا الشهر
        public int monthly_returns_count { get; set; }

        public int monthly_returns_boxes { get; set; }

        // =====================================
        // أفضل العناصر
        // =====================================

        public BestMarketerItem? best_marketer { get; set; }

        public BestCustomerItem? best_customer { get; set; }

        public BestSupplierItem? best_supplier { get; set; }

        // =====================================
        // المنتجات
        // =====================================

        public ProductStatsItem? most_sold_product { get; set; }

        public ProductStatsItem? most_produced_product { get; set; }

        // =====================================
        // الخزنات
        // =====================================

        public List<CashBoxStatsItem> cash_boxes { get; set; } = new();
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
    // أفضل مسوق
    // =========================================

    public class BestMarketerItem
    {
        public int marketer_id { get; set; }

        public string marketer_name { get; set; } = "";

        public decimal total_sales { get; set; }

        public int orders_count { get; set; }
    }

    // =========================================
    // أفضل زبون
    // =========================================

    public class BestCustomerItem
    {
        public int customer_id { get; set; }

        public string customer_name { get; set; } = "";

        public decimal total_sales { get; set; }

        public int orders_count { get; set; }
    }

    // =========================================
    // أفضل مورد
    // =========================================

    public class BestSupplierItem
    {
        public int supplier_id { get; set; }

        public string supplier_name { get; set; } = "";

        public decimal total_purchases { get; set; }
    }

    // =========================================
    // المنتجات
    // =========================================

    public class ProductStatsItem
    {
        public int product_id { get; set; }

        public string product_name { get; set; } = "";

        public int quantity { get; set; }
    }
}