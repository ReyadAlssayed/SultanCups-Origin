using Microsoft.EntityFrameworkCore;
using SultanCups.Data;
using SultanCups.Models;

namespace SultanCups.Services
{
    public class FinanceService2
    {
        private readonly AppDbContext _context;

        public FinanceService2(AppDbContext context)
        {
            _context = context;
        }

        private void AddFinancialEvent(
     string type,
     string direction,
     decimal amount,
     int cashBoxId,
     int adminId,
     int refId,
     string refTable,
     int? personId,
     string? personName,
     string? paymentMethod = null, // 🔥 جديد
     string notes = "",
     int? itemId = null,
     string? itemName = null)
        {
            var adminName = _context.admins
                .Where(x => x.admin_id == adminId)
                .Select(x => x.full_name)
                .FirstOrDefault();

            _context.financial_events.Add(new FinancialEvent
            {
                event_type = type,
                direction = direction,
                amount = amount,
                cash_box_id = cashBoxId,

                payment_method = paymentMethod, // 🔥 مهم

                performed_by = adminId,
                admin_name_snapshot = adminName,

                ref_table = refTable,
                ref_id = refId,

                person_id = personId,
                person_name_snapshot = personName,

                item_id = itemId,
                item_name_snapshot = itemName,

                event_date = DateTime.UtcNow,
                notes = notes
            });
        }

        // ✅ إنشاء فاتورة
        public async Task<(bool success, string message)> AddOrder(
       Order order,
       List<OrderItem> items,
       List<PaymentInput> payments, // 🔥 جديد
       int adminId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                order.Items = new List<OrderItem>();

                var productIds = items.Select(i => i.product_id).ToList();

                var stocks = await _context.product_stock
                    .Where(s => productIds.Contains(s.product_id))
                    .ToDictionaryAsync(s => s.product_id);

                // تحقق المخزون
                foreach (var item in items)
                {
                    if (!stocks.ContainsKey(item.product_id))
                        return (false, $"المنتج غير موجود (ID={item.product_id})");

                    if (stocks[item.product_id].quantity < item.quantity)
                        return (false, $"المخزون غير كافي (المتوفر={stocks[item.product_id].quantity})");
                }

                // حفظ الفاتورة
                _context.orders.Add(order);
                await _context.SaveChangesAsync();

                // حفظ الأصناف + خصم المخزون
                foreach (var item in items)
                {
                    item.order_id = order.order_id;

                    var productionCost = await _context.production
                        .Where(x =>
                            x.product_id == item.product_id &&
                            x.created_at <= order.created_at)
                        .OrderByDescending(x => x.created_at)
                        .Select(x => x.box_cost)
                        .FirstOrDefaultAsync();

                    if (item.production_cost <= 0)
                    {
                        item.production_cost = productionCost;
                    }

                    _context.order_items.Add(item);

                    stocks[item.product_id].quantity -= item.quantity;
                }

                await _context.SaveChangesAsync();

                // =========================================
                // 🔥 جلب اسم الشخص
                // =========================================
                string personName = "";

                if (order.person_type == "customer")
                {
                    personName = await _context.customers
                        .Where(c => c.customer_id == order.person_id)
                        .Select(c => c.name)
                        .FirstOrDefaultAsync();
                }
                else if (order.person_type == "marketer")
                {
                    personName = await _context.marketers
                        .Where(m => m.marketer_id == order.person_id)
                        .Select(m => m.name)
                        .FirstOrDefaultAsync();
                }

                // 🔥 هنا
                personName = order.person_type == "customer"
                    ? $"{personName} (زبون)"
                    : $"{personName} (مسوق)";

                // =========================================
                // 🔥 جلب أول منتج
                // =========================================
                var productIdsForSnapshot = items
    .Select(x => x.product_id)
    .Distinct()
    .Take(2)
    .ToList();

                var productNamesForSnapshot = await _context.products
                    .Where(p => productIdsForSnapshot.Contains(p.product_id))
                    .Select(p => p.name)
                    .ToListAsync();

                string itemSnapshotName =
                    string.Join(" + ", productNamesForSnapshot);

                var firstItemId =
                    productIdsForSnapshot.FirstOrDefault();

                // =========================================
                // 🔥 تسجيل الحركة المالية
                // =========================================
                if (payments != null && payments.Any() && order.cash_box_id != null)
                {
                    foreach (var p in payments)
                    {
                        if (p.amount <= 0) continue;

                        AddFinancialEvent(
                            "فاتورة بيع جديدة",
                            "IN",
                            p.amount,
                            order.cash_box_id,
                            adminId,
                            order.order_id,
                            "orders",
                            order.person_id,
                            personName,
                            p.method,
                            "دفعة فاتورة",
                            firstItemId,
string.IsNullOrWhiteSpace(itemSnapshotName)
    ? null
    : itemSnapshotName
                        );
                    }
                }

                await _context.SaveChangesAsync();

                if (order.person_type == "marketer" &&
    order.pay_commission_now)
                {
                    var totalQuantity = items.Sum(x => x.quantity);

                    var commissionAmount =
                        totalQuantity * order.commission_per_box;

                    AddFinancialEvent(
                        "دفع عمولة",
                        "OUT",
                        commissionAmount,
                        order.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        order.person_id,
                        personName,
                        null,
                        "صرف عمولة مسوق"
                    );

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return (true, order.order_id.ToString());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var msg = ex.InnerException?.Message ?? ex.Message;
                return (false, msg);
            }
        }

        public async Task<List<OrderView>> GetOrders(int page = 1, int pageSize = 20)
        {
            var query =
    from o in _context.orders.AsNoTracking()

    join c in _context.customers.AsNoTracking()
    on o.person_id equals c.customer_id into cg
    from c in cg.DefaultIfEmpty()

    join m in _context.marketers.AsNoTracking()
    on o.person_id equals m.marketer_id into mg
    from m in mg.DefaultIfEmpty()
    select new OrderView
    {
        order_id = o.order_id,

        person_id = o.person_id,

        person_type = o.person_type,

        is_cancelled = o.is_cancelled,

        person_name = o.person_type == "customer"
    ? c.name
    : m.name,

        is_special = o.person_type == "marketer"
         ? m.is_special
         : false,

        items_count = _context.order_items
         .Count(i => i.order_id == o.order_id),

        total_quantity = _context.order_items
         .Where(i => i.order_id == o.order_id)
         .Sum(i => (int?)i.quantity) ?? 0,

        total = _context.order_items
         .Where(i => i.order_id == o.order_id)
         .Sum(i => (decimal?)(i.quantity * i.unit_price)) ?? 0,

        discount_total = o.discount_total,

        net_total =
         ((_context.order_items
             .Where(i => i.order_id == o.order_id)
             .Sum(i => (decimal?)(i.quantity * i.unit_price)) ?? 0)
         - o.discount_total),

        commission_total = o.person_type == "marketer"
    ? ((_context.order_items
        .Where(i => i.order_id == o.order_id)
        .Sum(i => (int?)i.quantity) ?? 0)
        * o.commission_per_box)
    : 0,

        profit =

(
    (_context.order_items
        .Where(i => i.order_id == o.order_id)
        .Sum(i =>
            (decimal?)(i.quantity * i.unit_price)) ?? 0)

    -

    (

        (_context.order_items
            .Where(i => i.order_id == o.order_id)
            .Sum(i =>
                (decimal?)(i.quantity * i.production_cost)) ?? 0)

       +

(
    (
        o.person_type == "marketer"
        &&
        o.pay_commission_now
    )
    ? (
        (_context.order_items
            .Where(i => i.order_id == o.order_id)
            .Sum(i => (int?)i.quantity) ?? 0)

        * o.commission_per_box
      )
    : 0
)

)

),

        pay_commission_now = o.pay_commission_now,

        paid_amount =
_context.financial_events
    .Where(x =>
        x.ref_table == "orders" &&
        x.ref_id == o.order_id &&
        x.payment_method != null)
             .Sum(x =>
                 x.direction == "IN"
                     ? (decimal?)x.amount
                     : -(decimal?)x.amount
             ) ?? 0,

        order_date = o.order_date
    };

            return await query
                .AsNoTracking()
                .OrderByDescending(x => x.order_id)
                .ToListAsync();
        }

        //جلب الخزنات النشطة
        public async Task<List<CashBox>> GetCashBoxes()
        {
            return await _context.cash_boxes
                .Where(c => c.is_active)
                .ToListAsync();
        }


        //جلب معلومات الفاتورة برقم القيد للفاتورة

        public async Task<Order?> GetOrderById(int id)
        {
            return await _context.orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.order_id == id);
        }


        //جلب دفعات الفاتورة
        public async Task<List<FinancialEvent>> GetPaymentsByOrder(int orderId)
        {
            return await _context.financial_events
                .Where(x =>
                    x.ref_table == "orders" &&
                    x.ref_id == orderId &&
                    x.payment_method != null) // 🔥 حذف شرط IN
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<int, string>> GetProductsDict()
        {
            return await _context.products
                .ToDictionaryAsync(p => p.product_id, p => p.name);
        }

        public async Task<(bool success, string message)> UpdateOrder(
       Order updated,
       List<OrderItem> newItems,
       List<PaymentInput> payments,
       int adminId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var order = await _context.orders
     .FirstOrDefaultAsync(x => x.order_id == updated.order_id);

                if (order == null)
                    return (false, "الفاتورة غير موجودة");

                if (order.is_cancelled)
                    return (false, "لا يمكن تعديل فاتورة ملغاة");

                // ✔ بعد التحقق فقط

                order.discount_total = updated.discount_total;
                order.cash_box_id = updated.cash_box_id;
                order.notes = updated.notes;

                var oldCommissionPerBox = order.commission_per_box;
                var oldPayCommissionNow = order.pay_commission_now;

                order.commission_per_box = updated.commission_per_box;

                order.order_date = updated.order_date;

                var oldItems = await _context.order_items
                    .Where(i => i.order_id == order.order_id)
                    .ToListAsync();


                var stock = await _context.product_stock
                    .ToDictionaryAsync(s => s.product_id);

                foreach (var oldItem in oldItems)
                {
                    var edited = newItems.FirstOrDefault(x =>
                        x.product_id == oldItem.product_id);

                    if (edited == null)
                        continue;

                    // ❌ منع إنقاص الكمية
                    if (edited.quantity < oldItem.quantity)
                        return (false, "لا يمكن إنقاص الكمية من التعديل، استخدم الراجع");

                    // ✔ زيادة فقط
                    var diff = edited.quantity - oldItem.quantity;

                    if (diff > 0)
                    {
                        if (!stock.ContainsKey(oldItem.product_id))
                            return (false, "المنتج غير موجود");

                        if (stock[oldItem.product_id].quantity < diff)
                            return (false, "المخزون غير كافي");

                        stock[oldItem.product_id].quantity -= diff;
                    }

                    // ✔ تحديث السعر والكمية والقيمة المرجعية
                    oldItem.quantity = edited.quantity;
                    oldItem.unit_price = edited.unit_price;
                    oldItem.production_cost = edited.production_cost;
                }

                decimal oldTotal = oldItems.Sum(i => i.quantity * i.unit_price);
                decimal newTotal = newItems.Sum(i => i.quantity * i.unit_price);

                decimal newNet = newTotal - updated.discount_total;



                var existingIds = oldItems
     .Select(x => x.product_id)
     .ToHashSet();

                var addedItems = newItems
                    .Where(x => !existingIds.Contains(x.product_id))
                    .ToList();

                foreach (var item in addedItems)
                {
                    if (item.product_id <= 0)
                        return (false, "منتج غير صالح");

                    if (!stock.ContainsKey(item.product_id))
                        return (false, "منتج غير موجود");

                    if (stock[item.product_id].quantity < item.quantity)
                        return (false, "المخزون غير كافي");
                }

                foreach (var item in addedItems)
                {
                    stock[item.product_id].quantity -= item.quantity;
                }

                await _context.SaveChangesAsync();

                foreach (var item in addedItems)
                {
                    var productionCost = await _context.production
                        .Where(x =>
                            x.product_id == item.product_id &&
                            x.created_at <= order.created_at)
                        .OrderByDescending(x => x.created_at)
                        .Select(x => x.box_cost)
                        .FirstOrDefaultAsync();

                    _context.order_items.Add(new OrderItem
                    {
                        order_id = order.order_id,
                        product_id = item.product_id,
                        quantity = item.quantity,
                        unit_price = item.unit_price,
                        production_cost = item.production_cost > 0
    ? item.production_cost
    : productionCost
                    });
                }

                await _context.SaveChangesAsync();

                // =========================
                // 🔥 الشخص (قديم → جديد)
                // =========================
                string oldPersonName = "";
                string newPersonName = "";

                if (order.person_type == "customer")
                {
                    oldPersonName = await _context.customers
                        .Where(c => c.customer_id == order.person_id)
                        .Select(c => c.name)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    oldPersonName = await _context.marketers
                        .Where(m => m.marketer_id == order.person_id)
                        .Select(m => m.name)
                        .FirstOrDefaultAsync();
                }

                if (updated.person_type == "customer")
                {
                    newPersonName = await _context.customers
                        .Where(c => c.customer_id == updated.person_id)
                        .Select(c => c.name)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    newPersonName = await _context.marketers
                        .Where(m => m.marketer_id == updated.person_id)
                        .Select(m => m.name)
                        .FirstOrDefaultAsync();
                }
                string oldLabel = order.person_type == "customer"
    ? $"{oldPersonName} (زبون)"
    : $"{oldPersonName} (مسوق)";

                // 🔥 بعد حفظ القديم
                order.person_id = updated.person_id;
                order.person_type = updated.person_type;

                string newLabel = updated.person_type == "customer"
                    ? $"{newPersonName} (زبون)"
                    : $"{newPersonName} (مسوق)";

                // 🔥 جلب snapshot من أول حركة
                var firstEvent = await _context.financial_events
                    .Where(x => x.ref_table == "orders" && x.ref_id == order.order_id)
                    .OrderBy(x => x.event_id)
                    .FirstOrDefaultAsync();

                string snapshotName = firstEvent?.person_name_snapshot ?? oldLabel;

                // 🔥 المقارنة الصحيحة
                bool sameAsSnapshot =
     snapshotName?.Trim() == newLabel?.Trim();

                // ✔ اسم عادي (بدون سهم)
                string personName = newLabel;

                // ✔ اسم فيه سهم (فقط للتعديل الحقيقي)
                string personChangeLabel = sameAsSnapshot
                    ? newLabel
                    : $"{snapshotName} ← {newLabel}";

                // =========================
                // 🔥 المنتج (1 أو 2)
                // =========================
                var oldProductIds = oldItems.Select(x => x.product_id).Distinct().Take(2).ToList();
                var newProductIds = newItems.Select(x => x.product_id).Distinct().Take(2).ToList();

                var oldNames = await _context.products
                    .Where(p => oldProductIds.Contains(p.product_id))
                    .Select(p => p.name)
                    .ToListAsync();

                var newNames = await _context.products
                    .Where(p => newProductIds.Contains(p.product_id))
                    .Select(p => p.name)
                    .ToListAsync();

                string itemName;

                var oldSet = oldProductIds.OrderBy(x => x).ToList();
                var newSet = newProductIds.OrderBy(x => x).ToList();

                bool sameProducts = oldSet.SequenceEqual(newSet);

                if (sameProducts)
                {
                    // نفس المنتجات
                    itemName = string.Join(" + ", oldNames);
                }
                else
                {
                    // تغيرت المنتجات
                    itemName = $"{string.Join(" + ", oldNames)} → {string.Join(" + ", newNames)}";
                }

                var firstItemId = newProductIds.FirstOrDefault();

                // =========================
                // 🔥 الدفعات
                // =========================
                var oldPayments = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "orders" &&
                        x.ref_id == order.order_id &&
                        x.payment_method != null)
                    .ToListAsync();

                var oldPaid = oldPayments.Sum(x =>
                    x.direction == "IN" ? x.amount : -x.amount);

                var newPaymentsList = payments ?? new List<PaymentInput>();
                var newPaid = newPaymentsList.Sum(p => p.amount);

                if (newPaid > newNet)
                    return (false, "المبلغ المدفوع أكبر من الإجمالي");

                if (newPaymentsList.Any(p => p.amount < 0))
                    return (false, "لا يمكن إدخال مبلغ سالب");


                var finalCashBoxId = updated.cash_box_id;

                if (finalCashBoxId <= 0)
                    return (false, "اختر الخزنة");

                updated.cash_box_id = finalCashBoxId;

                HandleCashBoxChange(order, updated, oldPaid, personName, adminId);

                HandlePaymentDifferences(
    oldPayments,
    newPaymentsList,
    order,
    updated,
    personChangeLabel,
    adminId
);

                // =====================================
                // 🔥 فروقات العمولة
                // =====================================

                var oldQuantity = oldItems.Sum(x => x.quantity);
                var newQuantity = newItems.Sum(x => x.quantity);

                var oldCommission =
    oldQuantity * oldCommissionPerBox;

                var newCommission =
                    newQuantity * updated.commission_per_box;

                // ✔ هل كانت مصروفة؟
                bool oldPaidCommission = oldPayCommissionNow;

                // ✔ هل أصبحت مصروفة؟
                bool newPaidCommission = updated.pay_commission_now;

                // =====================================
                // 🔥 كانت غير مصروفة وأصبحت مصروفة
                // =====================================
                if (!oldPaidCommission && newPaidCommission)
                {
                    AddFinancialEvent(
                        "دفع عمولة",
                        "OUT",
                        newCommission,
                        updated.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        updated.person_id,
                        personName,
                        null,
                        "صرف عمولة بعد تعديل"
                    );
                }

                // =====================================
                // 🔥 كانت مصروفة وأصبحت غير مصروفة
                // =====================================
                else if (oldPaidCommission && !newPaidCommission)
                {
                    AddFinancialEvent(
                        "استرجاع عمولة",
                        "IN",
                        oldCommission,
                        updated.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        updated.person_id,
                        personName,
                        null,
                        "إلغاء صرف العمولة بعد تعديل"
                    );
                }

                // =====================================
                // 🔥 كانت مصروفة ومازالت مصروفة
                // نحسب الفرق فقط
                // =====================================
                else if (oldPaidCommission && newPaidCommission)
                {
                    var diff = newCommission - oldCommission;

                    if (diff > 0)
                    {
                        AddFinancialEvent(
                            "زيادة عمولة",
                            "OUT",
                            diff,
                            updated.cash_box_id,
                            adminId,
                            order.order_id,
                            "orders",
                            updated.person_id,
                            personName,
                            null,
                            "زيادة عمولة بعد تعديل"
                        );
                    }
                    else if (diff < 0)
                    {
                        AddFinancialEvent(
                            "استرجاع فرق عمولة",
                            "IN",
                            Math.Abs(diff),
                            updated.cash_box_id,
                            adminId,
                            order.order_id,
                            "orders",
                            updated.person_id,
                            personName,
                            null,
                            "استرجاع فرق عمولة بعد تعديل"
                        );
                    }
                }

                // جلب جميع الحركات المالية المرتبطة بالفاتورة
                var events = await _context.financial_events
                    .Where(x => x.ref_table == "orders" && x.ref_id == order.order_id)
                    .ToListAsync();

                foreach (var ev in events)
                {
                    // 1. تحديث المعرف فقط (مثل المشتريات تماماً) لربط الحركة بالشخص الجديد برمجياً
                    ev.person_id = updated.person_id;
                    ev.item_id = firstItemId;

                }

                order.pay_commission_now =
    updated.pay_commission_now;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (true, "تم تعديل الفاتورة ✔");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        //CancleOrder 

        public async Task<(bool success, string message)> CancelOrder(
    int orderId,
    int adminId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var order = await _context.orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.order_id == orderId);

                if (order == null)
                    return (false, "الفاتورة غير موجودة");

                if (order.is_cancelled)
                    return (false, "الفاتورة ملغاة مسبقًا");

                // =====================================
                // 🔥 إرجاع المنتجات للمخزون
                // =====================================
                var stock = await _context.product_stock
                    .ToDictionaryAsync(x => x.product_id);

                foreach (var item in order.Items)
                {
                    if (stock.ContainsKey(item.product_id))
                    {
                        stock[item.product_id].quantity += item.quantity;
                    }
                }

                // =====================================
                // 🔥 حساب المدفوع الحقيقي
                // =====================================
                var paidAmount =
    await _context.financial_events
        .Where(x =>
            x.ref_table == "orders" &&
            x.ref_id == order.order_id &&
            x.payment_method != null)
                        .SumAsync(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount
                        ) ?? 0;

                var paymentMethods = await _context.financial_events
    .Where(x =>
        x.ref_table == "orders" &&
        x.ref_id == order.order_id &&
        x.payment_method != null &&
        x.direction == "IN")
    .Select(x => x.payment_method!)
    .Distinct()
    .ToListAsync();

                if (paymentMethods.Count > 1 && paidAmount > 0)
                {
                    return (
                        false,
                        "هذه الفاتورة تحتوي على أكثر من طريقة دفع، قم أولاً بتعديل الدفعات من شاشة تعديل الفاتورة ثم أعد محاولة الإلغاء"
                    );
                }

                // =====================================
                // 🔥 اسم الشخص
                // =====================================
                string personName = "";

                if (order.person_type == "customer")
                {
                    personName = await _context.customers
                        .Where(c => c.customer_id == order.person_id)
                        .Select(c => c.name)
                        .FirstOrDefaultAsync() ?? "";
                }
                else
                {
                    personName = await _context.marketers
                        .Where(m => m.marketer_id == order.person_id)
                        .Select(m => m.name)
                        .FirstOrDefaultAsync() ?? "";
                }

                personName = order.person_type == "customer"
                    ? $"{personName} (زبون)"
                    : $"{personName} (مسوق)";

                // =====================================
                // 🔥 تسجيل استرجاع مالي
                // =====================================
                if (paidAmount > 0)
                {
                    AddFinancialEvent(
                        "حذف فاتورة بيع",
                        "OUT",
                        paidAmount,
                        order.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        order.person_id,
                        personName,
                        null,
                        "إلغاء الفاتورة وإرجاع الكميات إلى المخزون"
                    );
                }

                if (order.person_type == "marketer" &&
    order.pay_commission_now)
                {
                    var totalQuantity = order.Items.Sum(x => x.quantity);

                    var commissionAmount =
                        totalQuantity * order.commission_per_box;

                    AddFinancialEvent(
                        "استرجاع عمولة",
                        "IN",
                        commissionAmount,
                        order.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        order.person_id,
                        personName,
                        null,
                        "استرجاع عمولة بعد إلغاء الفاتورة"
                    );
                }

                // =====================================
                // 🔥 إلغاء الفاتورة
                // =====================================
                order.is_cancelled = true;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (true, "تم إلغاء الفاتورة ✔");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return (false, ex.InnerException?.Message ?? ex.Message);
            }
        }


        //ارجاع جزئي بمنتج من فاتورة مبيعات

        public async Task<List<ReturnItemInput>> GetOrderItemsForReturn(int orderId)
        {
            var order = await _context.orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.order_id == orderId);

            if (order == null || order.is_cancelled)
                return new List<ReturnItemInput>();

            var items = await _context.order_items
                .Where(x =>
                    x.order_id == orderId &&
                    x.quantity > 0)
                .ToListAsync();

            var productIds = items
                .Select(x => x.product_id)
                .Distinct()
                .ToList();

            var products = await _context.products
                .Where(x => productIds.Contains(x.product_id))
                .ToDictionaryAsync(
                    x => x.product_id,
                    x => x.name);

            return items
                .Select(x => new ReturnItemInput
                {
                    product_id = x.product_id,

                    product_name =
                        products.ContainsKey(x.product_id)
                            ? products[x.product_id]
                            : "غير معروف",

                    sold_quantity = x.quantity,

                    return_quantity = 0,

                    unit_price = x.unit_price,

                    commission_per_box =
                        order.person_type == "marketer"
                            ? order.commission_per_box
                            : 0
                })
                .OrderBy(x => x.product_name)
                .ToList();
        }

        public async Task<(bool success, string message)>
   ProcessPartialReturn(
       int orderId,
       List<ReturnItemInput> items,
       int adminId)
        {
            using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var order = await _context.orders
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x =>
                        x.order_id == orderId);

                if (order == null)
                    return (false, "الفاتورة غير موجودة");

                if (order.is_cancelled)
                    return (false, "الفاتورة ملغاة");

                var paymentMethods = await _context.financial_events
    .Where(x =>
        x.ref_table == "orders" &&
        x.ref_id == order.order_id &&
        x.payment_method != null &&
        x.direction == "IN")
    .Select(x => x.payment_method!)
    .Distinct()
    .ToListAsync();

                if (paymentMethods.Count > 1)
                {
                    return (
                        false,
                        "هذه الفاتورة تحتوي على أكثر من طريقة دفع، قم أولاً بتعديل الدفعات من شاشة تعديل الفاتورة ثم نفذ الراجع مره أخرى "
                    );
                }



                var validReturns = items
                    .Where(x => x.return_quantity > 0)
                    .ToList();

                if (!validReturns.Any())
                    return (false, "أدخل كمية راجعة");

                var stock = await _context.product_stock
                    .ToDictionaryAsync(x => x.product_id);

                string personName = "";

                if (order.person_type == "customer")
                {
                    personName =
                        await _context.customers
                            .Where(x =>
                                x.customer_id ==
                                order.person_id)
                            .Select(x => x.name)
                            .FirstOrDefaultAsync() ?? "";
                }
                else
                {
                    personName =
                        await _context.marketers
                            .Where(x =>
                                x.marketer_id ==
                                order.person_id)
                            .Select(x => x.name)
                            .FirstOrDefaultAsync() ?? "";
                }

                personName =
                    order.person_type == "customer"
                    ? $"{personName} (زبون)"
                    : $"{personName} (مسوق)";

                decimal totalRefund = 0;

                decimal orderPaidAmount =
     await _context.financial_events
         .Where(x =>
             x.ref_table == "orders" &&
             x.ref_id == order.order_id &&
             x.payment_method != null)
                         .SumAsync(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount
                        ) ?? 0;

                decimal totalCommissionReturn = 0;

                foreach (var item in validReturns)
                {
                    var orderItem =
                        order.Items.FirstOrDefault(x =>
                            x.product_id ==
                            item.product_id);

                    if (orderItem == null)
                        return (false, "منتج غير موجود بالفاتورة");

                    if (item.return_quantity >
                        orderItem.quantity)
                    {
                        return (
                            false,
                            $"الكمية المرجعة أكبر من الموجود للمنتج {item.product_name}"
                        );
                    }

                    // ==================================
                    // تحديث الفاتورة
                    // ==================================

                    orderItem.quantity -= item.return_quantity;

                    if (orderItem.quantity <= 0)
                    {
                        _context.order_items.Remove(orderItem);
                    }

                    // ==================================
                    // إرجاع للمخزون
                    // ==================================

                    if (stock.ContainsKey(item.product_id))
                    {
                        stock[item.product_id]
                            .quantity +=
                            item.return_quantity;
                    }

                    // ==================================
                    // تسجيل returns
                    // ==================================

                    _context.returns.Add(new Return
                    {
                        order_id = order.order_id,

                        product_id =
                            item.product_id,

                        returned_quantity =
                            item.return_quantity,

                        return_date =
                            DateTime.UtcNow
                    });

                    // ==================================
                    // المال الراجع
                    // ==================================

                    totalRefund +=
                        item.return_quantity
                        *
                        item.unit_price;

                    // ==================================
                    // عمولة المسوق
                    // ==================================

                    if (order.person_type == "marketer"
                        &&
                        order.pay_commission_now)
                    {
                        totalCommissionReturn +=
                            item.return_quantity
                            *
                            order.commission_per_box;
                    }
                }

                // ==================================
                // طرق الدفع المستخدمة
                // ==================================

                // ==================================
                // استرجاع مبلغ تلقائي
                // ==================================

                var returnNames = validReturns
    .Select(x => x.product_name)
    .Distinct()
    .ToList();

                string returnItemName =
                    string.Join(" + ", returnNames);

                var firstReturnItemId =
                    validReturns.FirstOrDefault()?.product_id;

                if (
                    paymentMethods.Count == 1
                    &&
                    totalRefund > 0
                    &&
                    orderPaidAmount > 0
                )
                {
                    var refundAmount =
                        Math.Min(
                            totalRefund,
                            orderPaidAmount
                        );

                    AddFinancialEvent(
    "استرجاع مبيعات",
    "OUT",
    refundAmount,
    order.cash_box_id,
    adminId,
    order.order_id,
    "orders",
    order.person_id,
    personName,
    paymentMethods.First(),
    "راجع جزئي وإرجاع مبلغ",
    firstReturnItemId,
    returnItemName
);
                }

                // ==================================
                // رجوع العمولة
                // ==================================

                if (totalCommissionReturn > 0)
                {
                    AddFinancialEvent(
                        "استرجاع عمولة",
                        "IN",
                        totalCommissionReturn,
                        order.cash_box_id,
                        adminId,
                        order.order_id,
                        "orders",
                        order.person_id,
                        personName,
                        null,
                        "استرجاع عمولة بسبب راجع جزئي"
                    );
                }

                await _context.SaveChangesAsync();

                var hasItems =
                    await _context.order_items
                        .AnyAsync(x =>
                            x.order_id == order.order_id);

                if (!hasItems)
                {
                    order.is_cancelled = true;

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return (true, "تم تنفيذ الراجع ✔");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return (
                    false,
                    ex.InnerException?.Message
                    ?? ex.Message
                );
            }
        }


        public async Task<decimal> GetLastProductionCost(int productId)
        {
            return await _context.production
                .Where(x => x.product_id == productId)
                .OrderByDescending(x => x.production_date)
                .ThenByDescending(x => x.production_id)
                .Select(x => x.box_cost)
                .FirstOrDefaultAsync();
        }
        private void HandleCashBoxChange(
    Order oldOrder,
    Order updated,
    decimal oldPaid,
    string personName,
    int adminId)
        {
            if (oldOrder.cash_box_id == updated.cash_box_id) return;

            if (oldPaid <= 0) return;

            // 🔻 إخراج من القديمة
            AddFinancialEvent(
                "تعديل فاتورة بيع",
                "OUT",
                oldPaid,
                oldOrder.cash_box_id,
                adminId,
                oldOrder.order_id,
                "orders",
                updated.person_id,
                personName,
                null,
                $"نقل من خزنة قديمة"
            );

            // 🔺 إدخال للجديدة
            AddFinancialEvent(
                "تعديل فاتورة بيع",
                "IN",
                oldPaid,
                updated.cash_box_id,
                adminId,
                oldOrder.order_id,
                "orders",
                updated.person_id,
                personName,
                null,
                $"نقل إلى خزنة جديدة"
            );
        }

        private void HandlePaymentDifferences(
    List<FinancialEvent> oldPayments,
    List<PaymentInput> newPayments,
    Order oldOrder,
    Order updated,
    string personName,
    int adminId)
        {
            var oldDict = oldPayments
                .GroupBy(p => p.payment_method ?? "cash")
                .ToDictionary(g => g.Key, g => g.Sum(x =>
                    x.direction == "IN" ? x.amount : -x.amount)); // ✔ هنا التصحيح

            var newDict = newPayments
                .GroupBy(p => p.method ?? "cash")
                .ToDictionary(g => g.Key, g => g.Sum(x => x.amount));

            var allMethods = oldDict.Keys.Union(newDict.Keys);

            foreach (var method in allMethods)
            {
                var oldAmount = oldDict.ContainsKey(method) ? oldDict[method] : 0;
                var newAmount = newDict.ContainsKey(method) ? newDict[method] : 0;

                var diff = newAmount - oldAmount;

                if (diff == 0) continue;

                var methodName = method switch
                {
                    "cash" => "نقدي",
                    "card" => "بطاقة",
                    "transfer" => "تحويل",
                    "check" => "شيك",
                    _ => method
                };

                if (diff > 0)
                {
                    // زيادة دفع
                    AddFinancialEvent(
                        "تعديل فاتورة بيع",
                        "IN",
                        diff,
                        updated.cash_box_id,
                        adminId,
                        oldOrder.order_id,
                        "orders",
                        updated.person_id,
                        personName,
                        method,
                        $"زاد {methodName} {diff}"
                    );
                }
                else
                {
                    // استرجاع
                    AddFinancialEvent(
                        "تعديل فاتورة بيع",
                        "OUT",
                        Math.Abs(diff),
                        oldOrder.cash_box_id,
                        adminId,
                        oldOrder.order_id,
                        "orders",
                        updated.person_id,
                        personName,
                        method,
                        $"نقص {methodName} {Math.Abs(diff)}"
                    );
                }
            }
        }

        public async Task<List<Order>> GetOrdersRaw()
        {
            return await _context.orders
    .AsNoTracking()
    .ToListAsync();
        }

        public async Task<List<OrderItem>> GetOrderItems()
        {
            return await _context.order_items
    .AsNoTracking()
    .ToListAsync();
        }

        public async Task<List<Product>> GetProducts()
        {
            return await _context.products
    .AsNoTracking()
    .ToListAsync();
        }

        public async Task<List<DebtView>> GetDebts()
        {
            var query =
                from o in _context.orders.AsNoTracking()

                join c in _context.customers.AsNoTracking()
on o.person_id equals c.customer_id into cg
                from c in cg.DefaultIfEmpty()

                join m in _context.marketers.AsNoTracking()
on o.person_id equals m.marketer_id into mg
                from m in mg.DefaultIfEmpty()

                let total = _context.order_items
                    .Where(i => i.order_id == o.order_id)
                    .Sum(i => (decimal?)i.quantity * i.unit_price) ?? 0

                let net = total - o.discount_total

                // 🔥 المدفوع الحقيقي
                let paid = _context.financial_events
     .Where(x =>
         x.ref_table == "orders" &&
         x.ref_id == o.order_id &&
         x.payment_method != null)
     .Sum(x =>
        x.direction == "IN"
            ? (decimal?)x.amount
            : -(decimal?)x.amount
    ) ?? 0

                let remaining = net - paid

                where remaining > 0 && !o.is_cancelled

                select new DebtView
                {
                    order_id = o.order_id,
                    person_name = o.person_type == "customer" ? c.name : m.name,
                    person_type = o.person_type,
                    order_date = o.order_date,
                    net_total = net,
                    paid_amount = paid, // ✔ الصحيح
                    remaining = remaining,
                    status = paid == 0 ? "دين كامل" : "خالص جزئي"
                };

            return await query
    .AsNoTracking()
    .OrderByDescending(x => x.order_id)
    .ToListAsync();
        }

        //

        public async Task<List<Customer>> GetCustomers()
        {
            return await _context.customers
    .AsNoTracking()
    .ToListAsync();
        }

        public async Task<List<Marketer>> GetMarketers()
        {
            return await _context.marketers
    .AsNoTracking()
    .ToListAsync();
        }

        public async Task<List<MarketerCommissionView>> GetMarketerCommissions()
        {
            var orders = await _context.orders
                .AsNoTracking()
                .Where(o => o.person_type == "marketer" && !o.is_cancelled)
                .Include(o => o.Items)
                .ToListAsync();

            var marketers = await _context.marketers
                .AsNoTracking()
                .ToDictionaryAsync(
                    m => m.marketer_id,
                    m => new
                    {
                        m.name,
                        m.is_special
                    });

            var result = orders.Select(o =>
            {
                var totalQuantity = o.Items.Sum(i => i.quantity);

                var orderTotal = o.Items.Sum(i => i.quantity * i.unit_price);

                var netTotal = orderTotal - o.discount_total;

                var commissionTotal = totalQuantity * o.commission_per_box;

                var marketerData = marketers.ContainsKey(o.person_id)
                    ? marketers[o.person_id]
                    : null;

                var marketerName = marketerData?.name ?? "غير معروف";

                bool canPayCommission = o.pay_commission_now;

                return new MarketerCommissionView
                {
                    order_id = o.order_id,

                    marketer_name = marketerName,

                    order_total = netTotal,

                    commission_total = commissionTotal,

                    commission_status = canPayCommission
    ? "محتسبة"
    : "غير محتسبة",

                    order_date = o.order_date
                };
            })
            .OrderByDescending(x => x.order_id)
            .ToList();

            return result;
        }

        //جلب المرتجعات في صفحة الراجع

        public async Task<List<ReturnView>> GetReturns()
        {
            var products = await _context.products
                .ToDictionaryAsync(
                    x => x.product_id,
                    x => x.name);

            var returns = await _context.returns
                .AsNoTracking()
                .OrderByDescending(x => x.return_id)
                .ToListAsync();

            return returns
                .Select(x => new ReturnView
                {
                    return_id = x.return_id,

                    order_id = x.order_id,

                    product_id = x.product_id,

                    returned_quantity =
                        x.returned_quantity,

                    return_date =
                        x.return_date,

                    product_name =
                        products.ContainsKey(x.product_id)
                            ? products[x.product_id]
                            : "غير معروف"
                })
                .ToList();
        }
    }
}