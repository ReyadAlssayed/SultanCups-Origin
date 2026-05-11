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

            // =====================================
            // إجمالي السيولة الحقيقية
            // =====================================

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

            // =====================================
            // أرباح الفواتير
            // =====================================

            var orders =
                await _context.orders
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x => !x.is_cancelled)
                    .ToListAsync();

            decimal totalProfit = 0;
            decimal totalLoss = 0;
            decimal totalDebts = 0;

            foreach (var order in orders)
            {
                decimal sales =
                    order.Items.Sum(i =>
                        i.unit_price * i.quantity);

                decimal cost =
                    order.Items.Sum(i =>
                        i.production_cost * i.quantity);

                decimal commission =
    order.person_type == "marketer"
        ? order.Items.Sum(i => i.quantity)
            * order.commission_per_box
        : 0;

                decimal profit =
                    sales
                    - cost
                    - commission
                    - order.discount_total;

                if (profit >= 0)
                    totalProfit += profit;
                else
                    totalLoss += Math.Abs(profit);

                decimal paid =
                    await _context.financial_events
                        .Where(x =>
                            x.ref_table == "orders"
                            &&
                            x.ref_id == order.order_id)
                        .SumAsync(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount
                        ) ?? 0;

                decimal remain =
                    (sales - order.discount_total)
                    - paid;

                if (remain > 0)
                    totalDebts += remain;
            }

            stats.total_orders_profit = totalProfit;
            stats.total_orders_loss = totalLoss;
            stats.total_debts = totalDebts;

            // =====================================
            // الداخل هذا الشهر
            // =====================================

            stats.monthly_in =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.direction == "IN"
                        &&
                        x.event_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // الخارج هذا الشهر
            // =====================================

            stats.monthly_out =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.direction == "OUT"
                        &&
                        x.event_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // المبيعات المحصلة
            // =====================================

            stats.monthly_sales_collected =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.ref_table == "orders"
                        &&
                        x.direction == "IN"
                        &&
                        x.event_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // المشتريات
            // =====================================

            stats.monthly_purchases =
                await _context.purchases
                    .AsNoTracking()
                    .Where(x =>
                        x.purchase_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)
                        (
                            (x.quantity * x.unit_price)
                            +
                            x.customs_cost
                            +
                            x.local_transport_cost
                            +
                            x.shipping_cost
                        )) ?? 0;

            // =====================================
            // السلف
            // =====================================

            stats.monthly_loans =
                await _context.employee_loans
                    .AsNoTracking()
                    .Where(x =>
                        x.loan_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)x.loan_amount)
                    ?? 0;

            // =====================================
            // الرواتب المدفوعة
            // =====================================

            stats.salaries_paid =
                await _context.salaries
                    .AsNoTracking()
                    .Where(x =>
                        x.salary_date >= monthStart)
                    .SumAsync(x =>
                        (decimal?)x.paid_amount)
                    ?? 0;

            // =====================================
            // الرواتب المستحقة
            // =====================================

            stats.salaries_remaining =
                await _context.salaries
                    .AsNoTracking()
                    .Where(x => x.status != "خالص")
                    .SumAsync(x =>
                        (decimal?)
                        (x.amount - x.paid_amount))
                    ?? 0;

            // =====================================
            // العمولات المدفوعة
            // =====================================

            stats.commissions_paid =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.event_type.Contains("عمولة")
                        &&
                        x.direction == "OUT")
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // العمولات غير المدفوعة
            // =====================================

            stats.commissions_unpaid =
                await _context.orders
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x =>
                        x.person_type == "marketer"
                        &&
                        !x.pay_commission_now
                        &&
                        !x.is_cancelled)
                    .SumAsync(x =>
                        (decimal?)
                        (
                            x.Items.Sum(i => i.quantity)
                            * x.commission_per_box
                        )) ?? 0;

            // =====================================
            // عدد الموظفين
            // =====================================

            stats.employees_count =
                await _context.employees
                    .AsNoTracking()
                    .CountAsync();

            // =====================================
            // إجمالي الإنتاج
            // =====================================

            stats.monthly_production_quantity =
                await _context.production
                    .AsNoTracking()
                    .Where(x =>
                        x.production_date >= monthStart)
                    .SumAsync(x =>
                        (int?)x.box_count)
                    ?? 0;

            // =====================================
            // عدد المرجوعات
            // =====================================

            // عدد عمليات الراجع
            stats.monthly_returns_count =
                await _context.returns
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.return_date >= monthStart);

            // إجمالي الصناديق الراجعة
            stats.monthly_returns_boxes =
                await _context.returns
                    .AsNoTracking()
                    .Where(x => x.return_date >= monthStart)
                    .SumAsync(x => (int?)x.returned_quantity)
                ?? 0;

            // =====================================
            // أفضل مسوق
            // =====================================

            var bestMarketer =
                await _context.orders
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x =>
                        x.person_type == "marketer"
                        &&
                        !x.is_cancelled
                        &&
                        x.order_date >= monthStart)
                    .GroupBy(x => x.person_id)
                    .Select(g => new
                    {
                        marketer_id = g.Key,
                        total_sales =
                            g.Sum(x =>
                                x.Items.Sum(i =>
                                    i.quantity * i.unit_price)),
                        orders_count = g.Count()
                    })
                    .OrderByDescending(x => x.total_sales)
                    .FirstOrDefaultAsync();

            if (bestMarketer != null)
            {
                stats.best_marketer =
                    new BestMarketerItem
                    {
                        marketer_id =
                            bestMarketer.marketer_id,

                        marketer_name =
                            await _context.marketers
                                .Where(x =>
                                    x.marketer_id ==
                                    bestMarketer.marketer_id)
                                .Select(x => x.name)
                                .FirstOrDefaultAsync()
                            ?? "غير معروف",

                        total_sales =
                            bestMarketer.total_sales,

                        orders_count =
                            bestMarketer.orders_count
                    };
            }

            // =====================================
            // أفضل زبون
            // =====================================

            var bestCustomer =
                await _context.orders
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x =>
                        x.person_type == "customer"
                        &&
                        !x.is_cancelled
                        &&
                        x.order_date >= monthStart)
                    .GroupBy(x => x.person_id)
                    .Select(g => new
                    {
                        customer_id = g.Key,
                        total_sales =
                            g.Sum(x =>
                                x.Items.Sum(i =>
                                    i.quantity * i.unit_price)),
                        orders_count = g.Count()
                    })
                    .OrderByDescending(x => x.total_sales)
                    .FirstOrDefaultAsync();

            if (bestCustomer != null)
            {
                stats.best_customer =
                    new BestCustomerItem
                    {
                        customer_id =
                            bestCustomer.customer_id,

                        customer_name =
                            await _context.customers
                                .Where(x =>
                                    x.customer_id ==
                                    bestCustomer.customer_id)
                                .Select(x => x.name)
                                .FirstOrDefaultAsync()
                            ?? "غير معروف",

                        total_sales =
                            bestCustomer.total_sales,

                        orders_count =
                            bestCustomer.orders_count
                    };
            }

            // =====================================
            // أفضل مورد
            // =====================================

            var bestSupplier =
                await _context.purchases
                    .AsNoTracking()
                    .Where(x =>
                        x.purchase_date >= monthStart
                        &&
                        x.supplier_id != null)
                    .GroupBy(x => x.supplier_id)
                    .Select(g => new
                    {
                        supplier_id = g.Key!.Value,

                        total_purchases =
                            g.Sum(x =>
                                (x.quantity * x.unit_price)
                                +
                                x.customs_cost
                                +
                                x.local_transport_cost
                                +
                                x.shipping_cost)
                    })
                    .OrderByDescending(x =>
                        x.total_purchases)
                    .FirstOrDefaultAsync();

            if (bestSupplier != null)
            {
                stats.best_supplier =
                    new BestSupplierItem
                    {
                        supplier_id =
                            bestSupplier.supplier_id,

                        supplier_name =
                            await _context.suppliers
                                .Where(x =>
                                    x.supplier_id ==
                                    bestSupplier.supplier_id)
                                .Select(x => x.name)
                                .FirstOrDefaultAsync()
                            ?? "غير معروف",

                        total_purchases =
                            bestSupplier.total_purchases
                    };
            }

            // =====================================
            // المنتج الأكثر مبيعاً
            // =====================================

            var mostSold =
                await _context.order_items
                    .AsNoTracking()
                    .GroupBy(x => x.product_id)
                    .Select(g => new
                    {
                        product_id = g.Key,
                        quantity =
                            g.Sum(x => x.quantity)
                    })
                    .OrderByDescending(x => x.quantity)
                    .FirstOrDefaultAsync();

            if (mostSold != null)
            {
                stats.most_sold_product =
                    new ProductStatsItem
                    {
                        product_id =
                            mostSold.product_id,

                        product_name =
                            await _context.products
                                .Where(x =>
                                    x.product_id ==
                                    mostSold.product_id)
                                .Select(x => x.name)
                                .FirstOrDefaultAsync()
                            ?? "غير معروف",

                        quantity =
                            mostSold.quantity
                    };
            }

            // =====================================
            // المنتج الأكثر إنتاجاً
            // =====================================

            var mostProduced =
                await _context.production
                    .AsNoTracking()
                    .GroupBy(x => x.product_id)
                    .Select(g => new
                    {
                        product_id = g.Key,
                        quantity =
                            g.Sum(x => x.box_count)
                    })
                    .OrderByDescending(x => x.quantity)
                    .FirstOrDefaultAsync();

            if (mostProduced != null)
            {
                stats.most_produced_product =
                    new ProductStatsItem
                    {
                        product_id =
                            mostProduced.product_id,

                        product_name =
                            await _context.products
                                .Where(x =>
                                    x.product_id ==
                                    mostProduced.product_id)
                                .Select(x => x.name)
                                .FirstOrDefaultAsync()
                            ?? "غير معروف",

                        quantity =
                            mostProduced.quantity
                    };
            }

            // =====================================
            // الخزنات
            // =====================================

            var cashBoxes =
                await _context.cash_boxes
                    .AsNoTracking()
                    .Where(x => x.is_active)
                    .ToListAsync();

            foreach (var box in cashBoxes)
            {
                decimal monthlyIn =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id ==
                            box.cash_box_id
                            &&
                            x.direction == "IN"
                            &&
                            x.event_date >= monthStart)
                        .SumAsync(x =>
                            (decimal?)x.amount)
                    ?? 0;

                decimal monthlyOut =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id ==
                            box.cash_box_id
                            &&
                            x.direction == "OUT"
                            &&
                            x.event_date >= monthStart)
                        .SumAsync(x =>
                            (decimal?)x.amount)
                    ?? 0;

                decimal currentBalance =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id ==
                            box.cash_box_id)
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
                        cash_box_id =
                            box.cash_box_id,

                        cash_box_name =
                            box.name,

                        current_balance =
                            currentBalance,

                        monthly_in =
                            monthlyIn,

                        monthly_out =
                            monthlyOut
                    });
            }

            return stats;
        }
    }
}