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
                var firstProduct = await _context.products
                    .Where(p => p.product_id == items.First().product_id)
                    .Select(p => new { p.product_id, p.name })
                    .FirstOrDefaultAsync();

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
                            firstProduct?.product_id,
                            firstProduct != null ? $"{firstProduct.name} (منتج)" : null
                        );
                    }
                }

                await _context.SaveChangesAsync();

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

        paid_amount =
         _context.financial_events
             .Where(x =>
                 x.ref_table == "orders" &&
                 x.ref_id == o.order_id)
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
                order.person_id = updated.person_id;
                order.person_type = updated.person_type;
                order.discount_total = updated.discount_total;
                order.cash_box_id = updated.cash_box_id;
                order.notes = updated.notes;
                order.commission_per_box = updated.commission_per_box;
                order.order_date = updated.order_date; 

                var oldItems = await _context.order_items
                    .Where(i => i.order_id == order.order_id)
                    .ToListAsync();

                decimal oldTotal = oldItems.Sum(i => i.quantity * i.unit_price);
                decimal newTotal = newItems.Sum(i => i.quantity * i.unit_price);

                decimal newNet = newTotal - updated.discount_total;

                var stock = await _context.product_stock
                    .ToDictionaryAsync(s => s.product_id);

                foreach (var item in oldItems)
                {
                    if (stock.ContainsKey(item.product_id))
                        stock[item.product_id].quantity += item.quantity;
                }

                // ❌ كان ناقص هنا
                await _context.SaveChangesAsync();

                _context.order_items.RemoveRange(oldItems);

                // ❌ كان ناقص هنا
                await _context.SaveChangesAsync();

                foreach (var item in newItems)
                {
                    if (item.product_id <= 0)
                        return (false, "منتج غير صالح");

                    if (!stock.ContainsKey(item.product_id))
                        return (false, "منتج غير موجود");

                    if (stock[item.product_id].quantity < item.quantity)
                        return (false, "المخزون غير كافي");
                }

                foreach (var item in newItems)
                {
                    stock[item.product_id].quantity -= item.quantity;
                }

                await _context.SaveChangesAsync();

                foreach (var item in newItems)
                {
                    _context.order_items.Add(new OrderItem
                    {
                        order_id = order.order_id,
                        product_id = item.product_id,
                        quantity = item.quantity,
                        unit_price = item.unit_price
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
                            x.ref_id == order.order_id)
                        .SumAsync(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount
                        ) ?? 0;

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
                        $"استرجاع مبلغ فاتورة رقم {order.order_id}"
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
        x.ref_id == o.order_id)
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

                var paidAmount =
                    _context.financial_events
                        .Where(x =>
                            x.ref_table == "orders" &&
                            x.ref_id == o.order_id)
                        .Sum(x =>
                            x.direction == "IN"
                                ? (decimal?)x.amount
                                : -(decimal?)x.amount
                        ) ?? 0;

                bool canPayCommission =
                    marketerData != null &&
                    (
                        marketerData.is_special ||
                        paidAmount >= netTotal
                    );

                return new MarketerCommissionView
                {
                    order_id = o.order_id,

                    marketer_name = marketerName,

                    order_total = netTotal,

                    commission_total = commissionTotal,

                    commission_status = canPayCommission
                        ? "مستحقة"
                        : "معلقة",

                    order_date = o.order_date
                };
            })
            .OrderByDescending(x => x.order_id)
            .ToList();

            return result;
        }
    }
}