using Microsoft.EntityFrameworkCore;
using SultanCups.Data;
using SultanCups.Models;

namespace SultanCups.Services
{
    public class StatsAndArchiveService
    {
        private readonly AppDbContext _context;

        public StatsAndArchiveService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StatsView> GetDashboardStats()
        {
            var stats = new StatsView();

            var now = DateTime.UtcNow;

            var monthStart = new DateTime(
    now.Year,
    now.Month,
    1,
    0,
    0,
    0,
    DateTimeKind.Utc);

            // =========================================
            // الفواتير الشهرية
            // =========================================

            var monthlyOrders = await _context.orders
                .AsNoTracking()
                .Where(x =>
                    !x.is_cancelled &&
                    x.order_date >= monthStart)
                .Include(x => x.Items)
                .ToListAsync();

            // =========================================
            // عدد الفواتير
            // =========================================

            stats.monthly_orders_count =
                monthlyOrders.Count;

            // =========================================
            // عدد الصناديق
            // =========================================

            stats.monthly_boxes_sold =
                monthlyOrders
                    .SelectMany(x => x.Items)
                    .Sum(x => x.quantity);

            // =========================================
            // صافي الربح الشهري
            // =========================================

            decimal totalSales =
                monthlyOrders
                    .SelectMany(x => x.Items)
                    .Sum(x => x.quantity * x.unit_price);

            decimal totalProductionCost =
                monthlyOrders
                    .SelectMany(x => x.Items)
                    .Sum(x => x.quantity * x.production_cost);

            decimal totalCommissions =
                monthlyOrders
                    .Where(x =>
                        x.person_type == "marketer"
                        &&
                        x.pay_commission_now)
                    .Sum(x =>
                        x.Items.Sum(i => i.quantity)
                        * x.commission_per_box);

            decimal salaries =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.event_type == "دفع راتب"
                        &&
                        x.event_date >= monthStart)
                    .SumAsync(x => (decimal?)x.amount) ?? 0;

            decimal expenses =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        (
                            x.event_type == "مصروف آخر"
                            ||
                            x.event_type == "صرف شراء جديد"
                            ||
                            x.event_type == "دفع مشتريات"
                        )
                        &&
                        x.event_date >= monthStart)
                    .SumAsync(x => (decimal?)x.amount) ?? 0;

            stats.monthly_profit =
                totalSales
                -
                (
                    totalProductionCost
                    +
                    totalCommissions
                    +
                    salaries
                    +
                    expenses
                );

            // =========================================
            // إجمالي السيولة
            // =========================================

            stats.total_cash_balance =
                await _context.financial_events
                    .AsNoTracking()
                    .SumAsync(x =>
                        (decimal?)
                        (
                            x.direction == "IN"
                                ? x.amount
                                : -x.amount
                        )) ?? 0;

            // =========================================
            // الخزنات
            // =========================================

            var cashBoxes = await _context.cash_boxes
                .AsNoTracking()
                .Where(x => x.is_active)
                .ToListAsync();

            foreach (var box in cashBoxes)
            {
                decimal monthlyIn =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id == box.cash_box_id
                            &&
                            x.direction == "IN"
                            &&
                            x.event_date >= monthStart)
                        .SumAsync(x => (decimal?)x.amount) ?? 0;

                decimal monthlyOut =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id == box.cash_box_id
                            &&
                            x.direction == "OUT"
                            &&
                            x.event_date >= monthStart)
                        .SumAsync(x => (decimal?)x.amount) ?? 0;

                decimal currentBalance =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id == box.cash_box_id)
                        .SumAsync(x =>
                            (decimal?)
                            (
                                x.direction == "IN"
                                    ? x.amount
                                    : -x.amount
                            )) ?? 0;

                stats.cash_boxes.Add(
                    new CashBoxStatsItem
                    {
                        cash_box_id = box.cash_box_id,
                        cash_box_name = box.name,
                        current_balance = currentBalance,
                        monthly_in = monthlyIn,
                        monthly_out = monthlyOut
                    });
            }

            // =========================================
            // ديون الزبائن
            // =========================================

            var customerDebts = monthlyOrders
                .Where(x =>
                    x.person_type == "customer")
                .Select(x =>
                {
                    decimal total =
                        x.Items.Sum(i =>
                            i.quantity * i.unit_price)
                        - x.discount_total;

                    decimal paid =
                        _context.financial_events
                            .Where(f =>
                                f.ref_table == "orders"
                                &&
                                f.ref_id == x.order_id)
                            .Sum(f =>
                                f.direction == "IN"
                                    ? (decimal?)f.amount
                                    : -(decimal?)f.amount)
                        ?? 0;

                    return new
                    {
                        x.person_id,
                        debt = total - paid
                    };
                })
                .Where(x => x.debt > 0)
                .ToList();

            var customers =
                await _context.customers
                    .AsNoTracking()
                    .ToDictionaryAsync(
                        x => x.customer_id,
                        x => x.name);

            stats.customer_debts =
                customerDebts
                    .Select(x =>
                        new CustomerDebtItem
                        {
                            customer_id = x.person_id,
                            customer_name =
                                customers.ContainsKey(x.person_id)
                                    ? customers[x.person_id]
                                    : "غير معروف",

                            debt_amount = x.debt
                        })
                    .OrderByDescending(x => x.debt_amount)
                    .ToList();

            // =========================================
            // ديون المسوقين
            // =========================================

            var marketerDebts = monthlyOrders
                .Where(x =>
                    x.person_type == "marketer")
                .Select(x =>
                {
                    decimal total =
                        x.Items.Sum(i =>
                            i.quantity * i.unit_price)
                        - x.discount_total;

                    decimal paid =
                        _context.financial_events
                            .Where(f =>
                                f.ref_table == "orders"
                                &&
                                f.ref_id == x.order_id)
                            .Sum(f =>
                                f.direction == "IN"
                                    ? (decimal?)f.amount
                                    : -(decimal?)f.amount)
                        ?? 0;

                    return new
                    {
                        x.person_id,
                        debt = total - paid
                    };
                })
                .Where(x => x.debt > 0)
                .ToList();

            var marketers =
                await _context.marketers
                    .AsNoTracking()
                    .ToDictionaryAsync(
                        x => x.marketer_id,
                        x => x.name);

            stats.marketer_debts =
                marketerDebts
                    .Select(x =>
                        new MarketerDebtItem
                        {
                            marketer_id = x.person_id,

                            marketer_name =
                                marketers.ContainsKey(x.person_id)
                                    ? marketers[x.person_id]
                                    : "غير معروف",

                            debt_amount = x.debt
                        })
                    .OrderByDescending(x => x.debt_amount)
                    .ToList();

            // =========================================
            // إجمالي الديون
            // =========================================

            stats.total_debts =
                stats.customer_debts.Sum(x => x.debt_amount)
                +
                stats.marketer_debts.Sum(x => x.debt_amount);

            // =========================================
            // المنتجات الناقصة
            // =========================================

            stats.low_stock_products =
                await _context.product_stock
                    .AsNoTracking()
                    .Where(x => x.quantity <= 20)
                    .Include(x => x.Product)
                    .Select(x =>
                        new LowStockItem
                        {
                            product_id = x.product_id,
                            product_name = x.Product.name,
                            current_quantity = x.quantity
                        })
                    .OrderBy(x => x.current_quantity)
                    .ToListAsync();

            // =========================================
            // أفضل المسوقين
            // =========================================

            stats.top_marketers =
                monthlyOrders
                    .Where(x =>
                        x.person_type == "marketer")
                    .GroupBy(x => x.person_id)
                    .Select(g =>
                        new TopMarketerItem
                        {
                            marketer_id = g.Key,

                            marketer_name =
                                marketers.ContainsKey(g.Key)
                                    ? marketers[g.Key]
                                    : "غير معروف",

                            orders_count = g.Count(),

                            total_sales =
                                g.Sum(x =>
                                    x.Items.Sum(i =>
                                        i.quantity * i.unit_price)
                                    - x.discount_total),

                            total_commissions =
                                g.Sum(x =>
                                    x.pay_commission_now
                                        ? x.Items.Sum(i => i.quantity)
                                            * x.commission_per_box
                                        : 0)
                        })
                    .OrderByDescending(x => x.total_sales)
                    .Take(5)
                    .ToList();

            // =========================================
            // توزيع المبيعات
            // =========================================

            var totalBoxes =
                monthlyOrders
                    .SelectMany(x => x.Items)
                    .Sum(x => x.quantity);

            stats.sales_distribution =
                monthlyOrders
                    .SelectMany(x => x.Items)
                    .GroupBy(x => x.product_id)
                    .Select(g =>
                        new ProductSalesDistribution
                        {
                            product_name =
                                _context.products
                                    .FirstOrDefault(p =>
                                        p.product_id == g.Key)!.name,

                            quantity =
                                g.Sum(x => x.quantity),

                            percentage =
                                totalBoxes == 0
                                    ? 0
                                    : Math.Round(
                                        (
                                            (decimal)g.Sum(x => x.quantity)
                                            / totalBoxes
                                        ) * 100,
                                        2)
                        })
                    .OrderByDescending(x => x.quantity)
                    .ToList();

            return stats;
        }
    }
}