namespace SultanCups.Models
{
    public class StatsView
    {
        // =====================================
        // البطاقات الرئيسية
        // =====================================

        // إجمالي السيولة الحقيقية
        public decimal total_cash_balance { get; set; }

        // صافي الوضع المالي الحقيقي الحالي
        //  كل الداخل (بما في دلك الرصيد الابتدائي)- كل الخارج
        public decimal real_financial_balance { get; set; }

        // إجمالي الديون الحالية
        public decimal total_debts { get; set; }

        // إجمالي الداخل منذ آخر جرد
        public decimal total_in { get; set; }

        // إجمالي الخارج منذ آخر جرد
        public decimal total_out { get; set; }

        // إجمالي المبيعات المحصلة منذ آخر جرد
        public decimal total_sales_collected { get; set; }

        // إجمالي المشتريات منذ آخر جرد
        public decimal total_purchases { get; set; }

        // إجمالي السلف منذ آخر جرد
        public decimal total_loans { get; set; }
        // الرواتب المدفوعة منذ آخر جرد
        public decimal salaries_paid { get; set; }

        // الرواتب المستحقة الحالية
        public decimal salaries_remaining { get; set; }

        // العمولات المدفوعة منذ آخر جرد
        public decimal commissions_paid { get; set; }

        // العمولات غير المدفوعة الحالية
        public decimal commissions_unpaid { get; set; }

        // عدد الموظفين
        public int employees_count { get; set; }

        // إجمالي الإنتاج منذ آخر جرد
        public int total_production_quantity { get; set; }

        // عدد عمليات المرجوعات منذ آخر جرد
        public int total_returns_count { get; set; }

        // عدد الصناديق الراجعة منذ آخر جرد
        public int total_returns_boxes { get; set; }
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

        public decimal total_in { get; set; }

        public decimal total_out { get; set; }
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