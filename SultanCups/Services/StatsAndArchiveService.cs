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

                    .OrderByDescending(x => x.to_date)

                    .Select(x => x.to_date)

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
         .Where(x =>
             x.event_date >= lastArchiveDate)
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
             x.event_type != "رصيد افتتاحي"
             &&
             x.event_date >= lastArchiveDate)
         .SumAsync(x =>
             (decimal?)
             (
                 x.direction == "IN"
                     ? x.amount
                     : -x.amount
             )) ?? 0;

            // =====================================
            // إجمالي الديون الحالية
            // ديون الفواتير فقط
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
             x.ref_id == order.order_id
             &&
             x.payment_method != null)
         .SumAsync(x =>
             x.direction == "IN"
                 ? (decimal?)x.amount
                 : -(decimal?)x.amount
         )
         ?? 0;

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
            // السلف غير المسددة
            // =====================================

            stats.loans_remaining =
     await _context.employee_loans
         .AsNoTracking()
         .Where(x => x.status != "خالص")
         .SumAsync(x =>
             (decimal?)
             (x.loan_amount - x.repaid_amount))
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
            // العمولات غير المدفوعة
            // =====================================

            stats.commissions_unpaid =
 await _context.orders
     .AsNoTracking()
     .Include(x => x.Items)
     .Where(x =>
         x.person_type == "marketer"
         &&
         !x.is_cancelled
         &&
         (
             x.pay_commission_now == false
         ))
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
         .Include(x => x.Order)
         .Where(x =>
             x.Order != null
             &&
             x.Order.order_date >= lastArchiveDate
             &&
             !x.Order.is_cancelled)
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
        .Where(x =>
            x.production_date >= lastArchiveDate)
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
             x.cash_box_id == box.cash_box_id
             &&
             x.event_date >= lastArchiveDate)
         .SumAsync(x =>
             (decimal?)
             (
                 x.direction == "IN"
                     ? x.amount
                     : -x.amount
             ))
     ?? 0;

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


        //

        public async Task<DateTime> GetLastArchiveDate()
        {
            return await _context.archive_cycles
                .AsNoTracking()
                .OrderByDescending(x => x.to_date)
                .Select(x => x.to_date)
                .FirstOrDefaultAsync();
        }

        public async Task<(bool success, string fromDate, string toDate)>
        ArchiveCurrentCycle(int adminId)
        {
            DateTime lastArchiveDate =
                await GetLastArchiveDate();

            if (lastArchiveDate == default)
            {
                lastArchiveDate =
    DateTime.SpecifyKind(
        new DateTime(2000, 1, 1),
        DateTimeKind.Utc);
            }

            var stats =
                await GetDashboardStats();

            var archive =
    new ArchiveCycle
    {
        from_date = lastArchiveDate,

        to_date = DateTime.UtcNow,

        archived_by = adminId,

        total_cash_balance =
                        stats.total_cash_balance,

        real_financial_balance =
                        stats.real_financial_balance,

        total_debts =
                        stats.total_debts,

        total_in =
                        stats.total_in,

        total_out =
                        stats.total_out,

        total_sales_collected =
                        stats.total_sales_collected,

        total_purchases =
                        stats.total_purchases,

        total_loans =
                        stats.total_loans,

        loans_remaining =
                stats.loans_remaining,

        salaries_remaining =
                        stats.salaries_remaining,


        commissions_unpaid =
                        stats.commissions_unpaid,

        employees_count =
                        stats.employees_count,

        total_production_quantity =
                        stats.total_production_quantity,

        total_returns_count =
                        stats.total_returns_count,

        total_returns_boxes =
                        stats.total_returns_boxes,

        best_marketer_name =
                        stats.best_marketer?.marketer_name,

        best_customer_name =
                        stats.best_customer?.customer_name,

        best_supplier_name =
                        stats.best_supplier?.supplier_name,

        most_sold_product_name =
                        stats.most_sold_product?.product_name,

        most_produced_product_name =
                        stats.most_produced_product?.product_name
    };

            _context.archive_cycles.Add(archive);

            await _context.SaveChangesAsync();

      

            return (
                true,
                lastArchiveDate.ToString("yyyy/MM/dd hh:mm tt"),
                DateTime.Now.ToString("yyyy/MM/dd hh:mm tt")
            );
        }

        //ترحيل المستحات المالية للجرد الجديد

        public async Task CleanArchivedData()
        {
            // =====================================
            // حذف السلف الخالصة
            // =====================================

            var finishedLoans = await _context.employee_loans
                .Where(x => x.status == "خالص")
                .ToListAsync();

            foreach (var loan in finishedLoans)
            {
                var loanEvents = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "employee_loans"
                        &&
                        x.ref_id == loan.loan_id)
                    .ToListAsync();

                _context.financial_events.RemoveRange(loanEvents);

                _context.employee_loans.Remove(loan);
            }

            // =====================================
            // حذف الرواتب الخالصة
            // =====================================

            var finishedSalaries = await _context.salaries
                .Where(x => x.status == "خالص")
                .ToListAsync();

            foreach (var salary in finishedSalaries)
            {
                var salaryEvents = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "salaries"
                        &&
                        x.ref_id == salary.salary_id)
                    .ToListAsync();

                _context.financial_events.RemoveRange(salaryEvents);

                _context.salaries.Remove(salary);
            }

            // =====================================
            // حذف الفواتير الخالصة
            // =====================================

            var orders = await _context.orders
                .Include(x => x.Items)
                .Where(x => !x.is_cancelled)
                .ToListAsync();

            foreach (var order in orders)
            {
                decimal total =
                    order.Items.Sum(i =>
                        i.quantity * i.unit_price);

                decimal paid =
                    await _context.financial_events
                        .Where(x =>
                            x.ref_table == "orders"
                            &&
                            x.ref_id == order.order_id
                            &&
                            x.payment_method != null)
                        .SumAsync(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount)
                    ?? 0;

                decimal remaining =
                    (total - order.discount_total)
                    - paid;

                bool hasUnpaidCommission =
                    order.person_type == "marketer"
                    &&
                    !order.pay_commission_now;

                // =====================================
                // إبقاء الفواتير الحية فقط
                // =====================================

                if (remaining > 0 || hasUnpaidCommission)
                    continue;

                // حذف العناصر

                var items = await _context.order_items
                    .Where(x =>
                        x.order_id == order.order_id)
                    .ToListAsync();

                _context.order_items.RemoveRange(items);

                // حذف المالية المرتبطة

                var orderEvents = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "orders"
                        &&
                        x.ref_id == order.order_id)
                    .ToListAsync();

                _context.financial_events.RemoveRange(orderEvents);

                // حذف الفاتورة

                _context.orders.Remove(order);
            }

            var purchaseEvents =
    await _context.financial_events
        .Where(x =>
            x.ref_table == "purchases")
        .ToListAsync();

            _context.financial_events
                .RemoveRange(purchaseEvents);



            var otherPurchaseEvents =
                await _context.financial_events
                    .Where(x =>
                        x.ref_table == "other_purchases")
                    .ToListAsync();

            _context.financial_events
                .RemoveRange(otherPurchaseEvents);

            // =====================================
            // تصفير الجداول التشغيلية
            // =====================================

            await _context.Database.ExecuteSqlRawAsync(
                @"TRUNCATE TABLE production RESTART IDENTITY CASCADE;");

            await _context.Database.ExecuteSqlRawAsync(
                @"TRUNCATE TABLE returns RESTART IDENTITY CASCADE;");

            await _context.Database.ExecuteSqlRawAsync(
                @"TRUNCATE TABLE purchases RESTART IDENTITY CASCADE;");

            await _context.Database.ExecuteSqlRawAsync(
                @"TRUNCATE TABLE other_purchases RESTART IDENTITY CASCADE;");

            await _context.Database.ExecuteSqlRawAsync(
                @"TRUNCATE TABLE audit_log RESTART IDENTITY CASCADE;");

            // =====================================
            // حفظ التعديلات
            // =====================================

            // حذف المالية القديمة غير المرتبطة
            var oldFinanceEvents =
                await _context.financial_events
                    .Where(x =>
                        x.ref_table == "cash_boxes")
                    .ToListAsync();

            _context.financial_events
                .RemoveRange(oldFinanceEvents);

            // حفظ كل شيء

            await _context.SaveChangesAsync();

        }

        }
}