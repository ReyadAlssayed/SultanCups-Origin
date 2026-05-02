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
                if (payments != null && payments.Any())
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
                from o in _context.orders
                join c in _context.customers on o.person_id equals c.customer_id into cg
                from c in cg.DefaultIfEmpty()
                join m in _context.marketers on o.person_id equals m.marketer_id into mg
                from m in mg.DefaultIfEmpty()
                select new OrderView
                {
                    order_id = o.order_id,
                    person_type = o.person_type,
                    discount_total = o.discount_total,
                    person_name = o.person_type == "customer" ? c.name : m.name,

                    items_count = _context.order_items.Count(i => i.order_id == o.order_id),

                    total = _context.order_items
    .Where(i => i.order_id == o.order_id)
    .Sum(i => (decimal?)(i.quantity * i.unit_price)) ?? 0,

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

                    paid_amount = o.paid_amount,   // 🔥 هذا الناقص
                    order_date = o.order_date
                };

            return await query
                .OrderByDescending(o => o.order_id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                    x.direction == "IN" &&
                    x.payment_method != null)
                .AsNoTracking()
                .ToListAsync();
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

                var oldItems = await _context.order_items
                    .Where(i => i.order_id == order.order_id)
                    .ToListAsync();

                // =====================================
                // 🔹 حساب الإجمالي
                // =====================================
                decimal oldTotal = oldItems.Sum(i => i.quantity * i.unit_price);
                decimal newTotal = newItems.Sum(i => i.quantity * i.unit_price);

                decimal oldNet = oldTotal - order.discount_total;
                decimal newNet = newTotal - updated.discount_total;

                // =====================================
                // 🔹 المخزون
                // =====================================
                var stock = await _context.product_stock
                    .ToDictionaryAsync(s => s.product_id);

                // رجع القديم
                foreach (var item in oldItems)
                {
                    if (stock.ContainsKey(item.product_id))
                        stock[item.product_id].quantity += item.quantity;
                }

                await _context.SaveChangesAsync();

                // حذف القديم
                _context.order_items.RemoveRange(oldItems);
                await _context.SaveChangesAsync();

                // تحقق الجديد
                foreach (var item in newItems)
                {
                    if (item.product_id <= 0)
                        return (false, "منتج غير صالح");

                    if (!stock.ContainsKey(item.product_id))
                        return (false, "منتج غير موجود");

                    if (stock[item.product_id].quantity < item.quantity)
                        return (false, "المخزون غير كافي");
                }

                // خصم الجديد
                foreach (var item in newItems)
                {
                    stock[item.product_id].quantity -= item.quantity;
                }

                await _context.SaveChangesAsync();

                // إضافة العناصر
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

                // =====================================
                // 🔥 اسم الشخص
                // =====================================
                string personName = "";

                if (updated.person_type == "customer")
                {
                    personName = await _context.customers
                        .Where(c => c.customer_id == updated.person_id)
                        .Select(c => c.name)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    personName = await _context.marketers
                        .Where(m => m.marketer_id == updated.person_id)
                        .Select(m => m.name)
                        .FirstOrDefaultAsync();
                }

                personName = updated.person_type == "customer"
                    ? $"{personName} (زبون)"
                    : $"{personName} (مسوق)";

                // =====================================
                // 🔥 جلب الدفعات القديمة
                // =====================================
                var oldPayments = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "orders" &&
                        x.ref_id == order.order_id &&
                        x.direction == "IN" &&
                        x.payment_method != null)
                    .ToListAsync();

                // 🔥 مجموع القديم
                var oldPaid = oldPayments.Sum(x => x.amount);

                // =====================================
                // 🔥 معالجة تغيير الخزنة
                // =====================================
                HandleCashBoxChange(
                    order,
                    updated,
                    oldPaid,
                    personName,
                    adminId
                );

                // =====================================
                // 🔥 معالجة فرق الدفعات
                // =====================================
                HandlePaymentDifferences(
                    oldPayments,
                    payments ?? new List<PaymentInput>(),
                    order,
                    updated,
                    personName,
                    adminId
                );

                await _context.SaveChangesAsync();

                // =====================================
                // 🔥 تحديث الفاتورة
                // =====================================
                order.person_id = updated.person_id;
                order.person_type = updated.person_type;
                order.discount_total = updated.discount_total;
                order.paid_amount = updated.paid_amount;
                order.cash_box_id = updated.cash_box_id;
                order.notes = updated.notes;
                order.commission_per_box = updated.commission_per_box;
                order.order_date = DateTime.SpecifyKind(updated.order_date, DateTimeKind.Utc);

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
                .ToDictionary(g => g.Key, g => g.Sum(x => x.amount));

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

                AddFinancialEvent(
                    "تعديل فاتورة بيع",
                    diff > 0 ? "IN" : "OUT",
                    Math.Abs(diff),
                    updated.cash_box_id,
                    adminId,
                    oldOrder.order_id,
                    "orders",
                    updated.person_id,
                    personName,
                    method,
                    diff > 0
                        ? $"زاد {methodName} {diff}"
                        : $"نقص {methodName} {Math.Abs(diff)}"
                );
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

        public async Task<List<DebtView>> GetDebts(int page = 1, int pageSize = 50)
        {
            var query =
                from o in _context.orders

                join c in _context.customers on o.person_id equals c.customer_id into cg
                from c in cg.DefaultIfEmpty()

                join m in _context.marketers on o.person_id equals m.marketer_id into mg
                from m in mg.DefaultIfEmpty()

                let total = _context.order_items
                    .Where(i => i.order_id == o.order_id)
                    .Sum(i => (decimal?)i.quantity * i.unit_price) ?? 0

                let net = total - o.discount_total
                let remaining = net - o.paid_amount

                where remaining > 0

                select new DebtView
                {
                    order_id = o.order_id,
                    person_name = o.person_type == "customer" ? c.name : m.name,
                    person_type = o.person_type,
                    order_date = o.order_date,
                    net_total = net,
                    paid_amount = o.paid_amount,
                    remaining = remaining,
                    status = o.paid_amount == 0 ? "دين كامل" : "خالص جزئي"
                };

            return await query
                .OrderByDescending(x => x.order_date)
                .Skip((page - 1) * pageSize)   // 🔥 هذا الجديد
                .Take(pageSize)                // 🔥 وهذا
                .ToListAsync();
        }

    }
}