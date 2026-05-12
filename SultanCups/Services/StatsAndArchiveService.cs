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

            // =====================================
            // آخر تاريخ جرد
            // =====================================

            DateTime lastArchiveDate =

                await _context.Set<ArchiveCycle>()

                    .AsNoTracking()

                    .OrderByDescending(x => x.archive_date)

                    .Select(x => x.archive_date)

                    .FirstOrDefaultAsync();

            if (lastArchiveDate == default)
            {
                lastArchiveDate =
                    new DateTime(
                        2000,
                        1,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc);
            }

            // =====================================
            // إجمالي السيولة الحقيقية
            // يشمل الرصيد الافتتاحي
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
            // صافي الوضع المالي الحقيقي
            // بدون الرصيد الافتتاحي
            // =====================================

            stats.real_financial_balance =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.event_type != "رصيد افتتاحي")
                    .SumAsync(x =>
                        (decimal?)
                        (
                            x.direction == "IN"
                                ? x.amount
                                : -x.amount
                        )) ?? 0;

            // =====================================
            // إجمالي الديون الحالية
            // =====================================

            var orders =
                await _context.orders
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x => !x.is_cancelled)
                    .ToListAsync();

            decimal totalDebts = 0;

            foreach (var order in orders)
            {
                decimal sales =
                    order.Items.Sum(i =>
                        i.unit_price * i.quantity);

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

            stats.total_debts = totalDebts;

            // =====================================
            // إجمالي الداخل منذ آخر جرد
            // =====================================

            stats.total_in =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.direction == "IN"
                        &&
                        x.event_date >= lastArchiveDate)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // إجمالي الخارج منذ آخر جرد
            // =====================================

            stats.total_out =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.direction == "OUT"
                        &&
                        x.event_date >= lastArchiveDate)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // إجمالي المبيعات المحصلة
            // =====================================

            stats.total_sales_collected =
                await _context.financial_events
                    .AsNoTracking()
                    .Where(x =>
                        x.ref_table == "orders"
                        &&
                        x.direction == "IN"
                        &&
                        x.event_date >= lastArchiveDate)
                    .SumAsync(x =>
                        (decimal?)x.amount) ?? 0;

            // =====================================
            // إجمالي المشتريات
            // =====================================

            stats.total_purchases =
                await _context.purchases
                    .AsNoTracking()
                    .Where(x =>
                        x.purchase_date >= lastArchiveDate)
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
            // إجمالي السلف
            // =====================================

            stats.total_loans =
                await _context.employee_loans
                    .AsNoTracking()
                    .Where(x =>
                        x.loan_date >= lastArchiveDate)
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
                        x.salary_date >= lastArchiveDate)
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
                        x.direction == "OUT"
                        &&
                        x.event_date >= lastArchiveDate)
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
                        !x.is_cancelled
                        &&
                        x.order_date >= lastArchiveDate)
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

            stats.total_production_quantity =
                await _context.production
                    .AsNoTracking()
                    .Where(x =>
                        x.production_date >= lastArchiveDate)
                    .SumAsync(x =>
                        (int?)x.box_count)
                    ?? 0;

            // =====================================
            // عدد عمليات المرجوعات
            // =====================================

            stats.total_returns_count =
                await _context.returns
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.return_date >= lastArchiveDate);

            // =====================================
            // عدد الصناديق الراجعة
            // =====================================

            stats.total_returns_boxes =
                await _context.returns
                    .AsNoTracking()
                    .Where(x =>
                        x.return_date >= lastArchiveDate)
                    .SumAsync(x =>
                        (int?)x.returned_quantity)
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
                        x.order_date >= lastArchiveDate)
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
                    .OrderByDescending(x =>
                        x.total_sales)
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
                        x.order_date >= lastArchiveDate)
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
                    .OrderByDescending(x =>
                        x.total_sales)
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
                        x.purchase_date >= lastArchiveDate
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
                    .OrderByDescending(x =>
                        x.quantity)
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
                    .OrderByDescending(x =>
                        x.quantity)
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
                decimal totalIn =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id ==
                            box.cash_box_id
                            &&
                            x.direction == "IN"
                            &&
                            x.event_date >= lastArchiveDate)
                        .SumAsync(x =>
                            (decimal?)x.amount)
                    ?? 0;

                decimal totalOut =
                    await _context.financial_events
                        .AsNoTracking()
                        .Where(x =>
                            x.cash_box_id ==
                            box.cash_box_id
                            &&
                            x.direction == "OUT"
                            &&
                            x.event_date >= lastArchiveDate)
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

                        total_in =
                            totalIn,

                        total_out =
                            totalOut
                    });
            }

            return stats;
        }
    }
}