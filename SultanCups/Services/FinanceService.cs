using Microsoft.EntityFrameworkCore;
using SultanCups.Components.Pages;
using SultanCups.Data;
using SultanCups.Models;
using System.Text.Json;

namespace SultanCups.Services
{
    public class FinanceService
    {
        private readonly AppDbContext _context;


    public FinanceService(AppDbContext context)
        {
            _context = context;
        }

        // =========================================
        // 🔹 Helpers (مشتركة)
        // =========================================

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
      string notes = "",
      int? itemId = null,
      string? itemName = null)
        {
            var personEvents = new[]
            {
        "دفع راتب",
        "استرجاع راتب",
        "دفع عمولة",
        "رصيد لصالح المسوق",
        "استخدام رصيد"
    };

            if (personEvents.Contains(type))
            {
                if (personId == null || string.IsNullOrWhiteSpace(personName))
                    throw new Exception("هذا النوع من العمليات يتطلب شخص");
            }

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


        private void AddAudit(
            string table,
            string operation,
            string recordId,
            object? oldData,
            object? newData,
            int adminId)
        {
            _context.audit_log.Add(new AuditLog
            {
                table_name = table,
                operation = operation,
                record_id = recordId,
                old_data = oldData != null ? JsonSerializer.Serialize(oldData) : null,
                new_data = newData != null ? JsonSerializer.Serialize(newData) : null,
                performed_by = adminId,
                performed_at = DateTime.UtcNow
            });
        }

        private void UpdateSalaryStatus(Salary salary)
        {
            if (salary.paid_amount == 0)
                salary.status = "غير خالص";
            else if (salary.paid_amount < salary.amount)
                salary.status = "خالص جزئي";
            else
                salary.status = "خالص";
        }
        private void UpdateLoanStatus(EmployeeLoan loan)
        {
            if (loan.repaid_amount == 0)
                loan.status = "غير خالص";
            else if (loan.repaid_amount < loan.loan_amount)
                loan.status = "خالص جزئي";
            else
                loan.status = "خالص";
        }

        //رصيد افتتاحي

        public async Task AddOpeningBalance(
       int cashBoxId,
       decimal amount,
       string? note,
       int adminId)
        {
            var cashBox = await _context.cash_boxes
                .FirstOrDefaultAsync(x =>
                    x.cash_box_id == cashBoxId);

            if (cashBox == null)
                throw new Exception("الخزنة غير موجودة");

            var admin = await _context.admins
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.admin_id == adminId);

            if (admin == null)
                throw new Exception("المستخدم غير موجود");

            var financialEvent = new FinancialEvent
            {
                event_type = "رصيد افتتاحي",

                direction = "IN",

                amount = amount,

                cash_box_id = cashBoxId,

                performed_by = adminId,

                admin_name_snapshot = admin.full_name,

                ref_table = "cash_boxes",

                ref_id = cashBoxId,

                event_date = DateTime.UtcNow,

                notes = string.IsNullOrWhiteSpace(note)
                    ? "رصيد افتتاحي للخزنة"
                    : note
            };

            _context.financial_events.Add(financialEvent);

            await _context.SaveChangesAsync();
        }
        // =========================================
        // 🔹 Employees
        // =========================================

        public async Task<List<Employee>> GetActiveEmployees()
        {
            return await _context.employees
                .Where(e => e.is_active)
                .ToListAsync();
        }

        // =========================================
        // 🔹 Salaries
        // =========================================

        public async Task<List<Salary>> GetSalaries()
        {
            return await _context.salaries
                .AsNoTracking()
                .Include(s => s.Employee)
                .Include(s => s.CashBox)
                .ToListAsync();
        }

        public async Task AddSalary(Salary salary, int adminId)
        {
            if (string.IsNullOrWhiteSpace(salary.salary_type))
                salary.salary_type = "راتب أساسي";

            if (salary.paid_amount <= 0)
                throw new Exception("يجب إدخال مبلغ مدفوع");

            var existing = await _context.salaries
                .FirstOrDefaultAsync(s =>
                    s.employee_id == salary.employee_id &&
                    s.salary_date.Year == salary.salary_date.Year &&
                    s.salary_date.Month == salary.salary_date.Month);

            if (existing != null)
                throw new Exception("تم صرف راتب لهذا الموظف خلال هذا الشهر، استخدم إضافة دفعة إذا كان هناك مبلغ متبقٍ");

            UpdateSalaryStatus(salary);

            salary.salary_date = DateTime.SpecifyKind(salary.salary_date, DateTimeKind.Utc);

            var balance = await GetBalanceFromView(salary.cash_box_id);

            if (salary.paid_amount > balance)
                throw new Exception("رصيد الخزنة غير كافٍ");

            _context.salaries.Add(salary);
            await _context.SaveChangesAsync();

            var employee = await _context.employees
                .AsNoTracking()
                .FirstAsync(e => e.employee_id == salary.employee_id);

            AddFinancialEvent(
                "دفع راتب",
                "OUT",
                salary.paid_amount,
                salary.cash_box_id,
                adminId,
                salary.salary_id,
                "salaries",
                salary.employee_id,
                employee.full_name,
                "دفع راتب عند الإنشاء"
            );

            await _context.SaveChangesAsync();
        }


        public async Task<bool> AddSalaryPayment(int salaryId, decimal amount, int adminId)
        {
            var salary = await _context.salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.salary_id == salaryId);

            if (salary == null) return false;

            var remaining = salary.amount - salary.paid_amount;

            var balance = await GetBalanceFromView(salary.cash_box_id);
            if (amount > balance) return false;

            if (amount <= 0 || amount > remaining) return false;

            salary.paid_amount += amount;

            UpdateSalaryStatus(salary);

            AddFinancialEvent(
                "دفع راتب",
                "OUT",
                amount,
                salary.cash_box_id,
                adminId,
                salary.salary_id,
                "salaries",
                salary.employee_id,
                salary.Employee.full_name,
                "إضافة دفعة راتب"
            );

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReverseSalary(int salaryId, decimal amount, int adminId)
        {
            var salary = await _context.salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.salary_id == salaryId);

            if (salary == null) return false;

            if (amount <= 0 || amount > salary.paid_amount) return false;

            salary.paid_amount -= amount;

            UpdateSalaryStatus(salary);

            AddFinancialEvent(
                "استرجاع راتب",
                "IN",
                amount,
                salary.cash_box_id,
                adminId,
                salary.salary_id,
                "salaries",
                salary.employee_id,
                salary.Employee.full_name,
                "استرجاع راتب"
            );

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateSalary(Salary updated, int adminId)
        {
            var salary = await _context.salaries
                .FirstOrDefaultAsync(x => x.salary_id == updated.salary_id);

            if (salary == null) return;

            // 🔥 منع تكرار راتب لنفس الموظف في نفس الشهر عند تغيير الموظف
            if (salary.employee_id != updated.employee_id)
            {
                var year = salary.salary_date.Year;
                var month = salary.salary_date.Month;

                var exists = await _context.salaries
                    .AnyAsync(s =>
                        s.employee_id == updated.employee_id &&
                        s.salary_id != salary.salary_id &&
                        s.salary_date.Year == year &&
                        s.salary_date.Month == month);

                if (exists)
                    throw new Exception("لا يمكن إضافة راتب لهذا الموظف، لديه راتب أساسي في نفس الشهر");
            }

            var oldData = new Dictionary<string, object>();
            var newData = new Dictionary<string, object>();

            if (salary.employee_id != updated.employee_id)
            {
                var oldEmp = await _context.employees.FindAsync(salary.employee_id);
                var newEmp = await _context.employees.FindAsync(updated.employee_id);

                oldData["employee"] = oldEmp?.full_name ?? "";
                newData["employee"] = newEmp?.full_name ?? "";
            }

            if (salary.amount != updated.amount)
            {
                oldData["amount"] = salary.amount;
                newData["amount"] = updated.amount;
            }

            if ((salary.notes ?? "") != (updated.notes ?? ""))
            {
                oldData["notes"] = salary.notes ?? "";
                newData["notes"] = updated.notes ?? "";
            }

            if (oldData.Count > 0)
            {
                AddAudit(
                    "salaries",
                    "UPDATE",
                    salary.salary_id.ToString(),
                    oldData,
                    newData,
                    adminId
                );
            }

            // 🔥 نقل الراتب بين الخزن
            if (salary.cash_box_id != updated.cash_box_id && salary.paid_amount > 0)
            {
                var balance = await GetBalanceFromView(updated.cash_box_id);

                if (salary.paid_amount > balance)
                    throw new Exception("رصيد الخزنة الجديدة غير كافي");

                var employee = await _context.employees
                    .FirstOrDefaultAsync(e => e.employee_id == salary.employee_id);

                AddFinancialEvent(
                    "استرجاع راتب",
                    "IN",
                    salary.paid_amount,
                    salary.cash_box_id,
                    adminId,
                    salary.salary_id,
                    "salaries",
                    salary.employee_id,
                    employee?.full_name,
                    "نقل الراتب من خزنة قديمة"
                );

                AddFinancialEvent(
                    "دفع راتب",
                    "OUT",
                    salary.paid_amount,
                    updated.cash_box_id,
                    adminId,
                    salary.salary_id,
                    "salaries",
                    salary.employee_id,
                    employee?.full_name,
                    "نقل الراتب إلى خزنة جديدة"
                );
            }

            salary.amount = updated.amount;
            salary.salary_type = updated.salary_type;
            salary.cash_box_id = updated.cash_box_id;
            salary.notes = updated.notes;
            salary.employee_id = updated.employee_id;
            salary.salary_date = DateTime.SpecifyKind(updated.salary_date, DateTimeKind.Utc);

            UpdateSalaryStatus(salary);

            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteSalary(int salaryId, int adminId)
        {
            var salary = await _context.salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.salary_id == salaryId);

            if (salary == null) return false;

            if (salary.paid_amount > 0)
            {
                AddFinancialEvent(
                    "استرجاع راتب",
                    "IN",
                    salary.paid_amount,
                    salary.cash_box_id,
                    adminId,
                    salary.salary_id,
                    "salaries",
                    salary.employee_id,
                    salary.Employee.full_name,
                    "حذف راتب - استرجاع كامل"
                );
            }

            var data = new
            {
                employee = salary.Employee.full_name,
                amount = salary.amount,
                paid = salary.paid_amount,
                notes = salary.notes
            };

            AddAudit(
                "salaries",
                "DELETE",
                salary.salary_id.ToString(),
                data,
                null,
                adminId
            );

            _context.salaries.Remove(salary);

            await _context.SaveChangesAsync();

            return true;
        }


        // =========================================    
        // 🔹 cash_boxes (الخزن)
        // =========================================

        public async Task<List<CashBox>> GetCashBoxes()
        {
            return await _context.cash_boxes
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<CashBox>> GetActiveCashBoxes()
        {
            return await _context.cash_boxes
                .AsNoTracking()
                .Where(c => c.is_active)
                .ToListAsync();
        }

        public async Task AddCashBox(CashBox cashBox)
        {
            _context.cash_boxes.Add(cashBox);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateCashBox(CashBox updated)
        {
            var box = await _context.cash_boxes
                .FirstOrDefaultAsync(c => c.cash_box_id == updated.cash_box_id);

            if (box == null)
                return false;

            box.name = updated.name;
            box.is_active = updated.is_active;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleCashBox(int id)
        {
            var box = await _context.cash_boxes
                .FirstOrDefaultAsync(c => c.cash_box_id == id);

            if (box == null)
                return false;

            box.is_active = !box.is_active;

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<CashBoxBalance>> GetCashBoxBalances()
        {
            return await _context.cash_box_balances.ToListAsync();
        }
        public async Task<decimal> GetBalanceFromView(int cashBoxId)
        {
            return await _context.cash_box_balances
                .Where(x => x.cash_box_id == cashBoxId)
                .Select(x => x.balance)
                .FirstOrDefaultAsync();
        }

        //جلب السجلات المالية كاملة

        public async Task<List<FinancialEvent>> GetFinancialEvents()
        {
            return await _context.financial_events
                .AsNoTracking()
                .Include(x => x.CashBox)
                .Include(x => x.Admin)
                .OrderByDescending(x => x.event_date)
                .ToListAsync();
        }

        public async Task DepositCashBox(
           int cashBoxId,
           decimal amount,
           string notes,
           int adminId)
        {
            if (cashBoxId <= 0)
                throw new Exception("اختر الخزنة");

            if (amount <= 0)
                throw new Exception("المبلغ غير صحيح");

            AddFinancialEvent(
                "إيداع خزنة",
                "IN",
                amount,
                cashBoxId,
                adminId,
                cashBoxId,
"cash_boxes",
                null,
                null,
                string.IsNullOrWhiteSpace(notes) ? "إيداع في الخزنة" : notes
            );

            AddAudit(
                "cash_boxes",
                "INSERT",
                cashBoxId.ToString(),
                null,
                new
                {
                    action = "deposit",
                    cashBoxId,
                    amount,
                    notes
                },
                adminId
            );

            await _context.SaveChangesAsync();
        }

        public async Task TransferBetweenCashBoxes(
            int fromCashBoxId,
            int toCashBoxId,
            decimal amount,
            string notes,
            int adminId)
        {
            if (fromCashBoxId <= 0 || toCashBoxId <= 0)
                throw new Exception("اختر الخزنات");

            if (fromCashBoxId == toCashBoxId)
                throw new Exception("لا يمكن التحويل لنفس الخزنة");

            if (amount <= 0)
                throw new Exception("المبلغ غير صحيح");

            var balance = await GetBalanceFromView(fromCashBoxId);

            if (amount > balance)
                throw new Exception("رصيد الخزنة المصدر غير كاف");

            var fromBox = await _context.cash_boxes
                .FirstOrDefaultAsync(x => x.cash_box_id == fromCashBoxId);

            var toBox = await _context.cash_boxes
                .FirstOrDefaultAsync(x => x.cash_box_id == toCashBoxId);

            AddFinancialEvent(
                "تحويل بين خزنات",
                "OUT",
                amount,
                fromCashBoxId,
                adminId,
                toCashBoxId,
                "cash_boxes",
                null,
                null,
                $"تحويل إلى {toBox?.name} | {notes}"
            );

            AddFinancialEvent(
                "تحويل بين خزنات",
                "IN",
                amount,
                toCashBoxId,
                adminId,
                fromCashBoxId,
                "cash_boxes",
                null,
                null,
                $"تحويل من {fromBox?.name} | {notes}"
            );

            AddAudit(
                "cash_boxes",
                "INSERT",
                fromCashBoxId.ToString(),
                null,
                new
                {
                    action = "transfer",
                    from = fromBox?.name,
                    to = toBox?.name,
                    amount,
                    notes
                },
                adminId
            );

            await _context.SaveChangesAsync();
        }



        // =========================================
        // 🔹 other_purchases (المصروفات الأخرى)
        // =========================================

        // جلب الكل
        public async Task<List<OtherPurchase>> GetOtherPurchases()
        {
            return await _context.other_purchases
                .AsNoTracking()
                .OrderByDescending(x => x.purchase_date)
                .ToListAsync();
        }

        // إضافة مصروف جديد
        public async Task AddOtherPurchase(OtherPurchase item, int adminId)
        {
            item.name = item.name.Trim();
            item.notes = item.notes?.Trim();
            item.purchase_date = DateTime.SpecifyKind(
                item.purchase_date,
                DateTimeKind.Utc);

            if (item.cost <= 0)
                throw new Exception("المبلغ يجب أن يكون أكبر من صفر");

            if (item.cash_box_id <= 0)
                throw new Exception("اختر الخزنة");

            var balance = await GetBalanceFromView(item.cash_box_id);

            if (item.cost > balance)
                throw new Exception("رصيد الخزنة غير كافٍ");

            _context.other_purchases.Add(item);
            await _context.SaveChangesAsync();

            AddFinancialEvent(
                "صرف مصروف جديد",
                "OUT",
                item.cost,
                item.cash_box_id,
                adminId,
               item.other_purchase_id,
"other_purchases",
null,
null,
"مصروف: " + item.name
            );

            await _context.SaveChangesAsync();
        }

        // تعديل (بدون تعديل المبلغ مباشرة)
        public async Task<bool> UpdateOtherPurchase(OtherPurchase updated, int adminId)
        {
            var p = await _context.other_purchases
                .FirstOrDefaultAsync(x => x.other_purchase_id == updated.other_purchase_id);

            if (p == null)
                return false;

            updated.purchase_date = DateTime.SpecifyKind(
                updated.purchase_date,
                DateTimeKind.Utc);

            // تغيير الخزنة = سجلين
            if (p.cash_box_id != updated.cash_box_id && p.cost > 0)
            {
                if (updated.cash_box_id <= 0)
                    throw new Exception("اختر الخزنة الجديدة");

                var balance = await GetBalanceFromView(updated.cash_box_id);

                if (p.cost > balance)
                    throw new Exception("رصيد الخزنة الجديدة غير كافٍ");

                AddFinancialEvent(
                    "نقل المصروف من خزنة قديمة",
                    "IN",
                    p.cost,
                    p.cash_box_id,
                    adminId,
                   p.other_purchase_id,
"other_purchases",
null,
null,
p.name
                );

                AddFinancialEvent(
                    "نقل المصروف إلى خزنة جديدة",
                    "OUT",
                    p.cost,
                    updated.cash_box_id,
                    adminId,
                   p.other_purchase_id,
"other_purchases",
null,
null,
p.name
                );
            }

            p.name = updated.name.Trim();
            p.quantity = updated.quantity;
            p.purchase_date = updated.purchase_date;
            p.notes = updated.notes?.Trim();
            p.cash_box_id = updated.cash_box_id;

            await _context.SaveChangesAsync();
            return true;
        }

        // زيادة مبلغ على المصروف
        public async Task<bool> AddOtherPurchasePayment(int id, decimal amount, int adminId)
        {
            var p = await _context.other_purchases
                .FirstOrDefaultAsync(x => x.other_purchase_id == id);

            if (p == null)
                return false;

            if (amount <= 0)
                return false;


            var balance = await GetBalanceFromView(p.cash_box_id);

            if (amount > balance)
                return false;

            p.cost += amount;

            AddFinancialEvent(
                "زيادة مبلغ على المصروف",
                "OUT",
                amount,
                p.cash_box_id,
                adminId,
               p.other_purchase_id,
"other_purchases",
null,
null,
p.name
            );

            await _context.SaveChangesAsync();
            return true;
        }

        // استرجاع مبلغ من المصروف
        public async Task<bool> ReverseOtherPurchasePayment(int id, decimal amount, int adminId)
        {
            var p = await _context.other_purchases
                .FirstOrDefaultAsync(x => x.other_purchase_id == id);

            if (p == null)
                return false;

            if (amount <= 0 || amount > p.cost)
                return false;


            p.cost -= amount;

            AddFinancialEvent(
                "استرجاع مبلغ من المصروف",
                "IN",
                amount,
                p.cash_box_id,
                adminId,
                p.other_purchase_id,
"other_purchases",
null,
null,
p.name
            );

            await _context.SaveChangesAsync();
            return true;
        }

        // حذف مصروف وإرجاع المبلغ
        public async Task<bool> DeleteOtherPurchase(int id, int adminId)
        {
            var p = await _context.other_purchases
                .FirstOrDefaultAsync(x => x.other_purchase_id == id);

            if (p == null)
                return false;

            if (p.cost > 0)
            {
                AddFinancialEvent(
                    "حذف مصروف وإرجاع المبلغ",
                    "IN",
                    p.cost,
                    p.cash_box_id,
                    adminId,
                   p.other_purchase_id,
"other_purchases",
null,
null,
p.name
                );
            }

            _context.other_purchases.Remove(p);

            await _context.SaveChangesAsync();
            return true;
        }

        // =========================================
        // 🔹 employee_loans (السلف)
        // =========================================

        public async Task<List<EmployeeLoan>> GetLoans()
        {
            return await _context.employee_loans
                .AsNoTracking()
                .Include(x => x.Employee)
                .Include(x => x.CashBox)
                .OrderByDescending(x => x.loan_date)
                .ToListAsync();
        }
        public async Task<decimal> GetEmployeeLoanRemaining(int employeeId)
        {
            return await _context.employee_loans
                .Where(x => x.employee_id == employeeId && x.status != "خالص")
                .Select(x => x.loan_amount - x.repaid_amount)
                .SumAsync();
        }

        public async Task AddLoan(EmployeeLoan loan, int adminId)
        {
            if (loan.employee_id <= 0)
                throw new Exception("اختر الموظف");

            if (loan.cash_box_id <= 0)
                throw new Exception("اختر الخزنة");

            if (loan.loan_amount <= 0)
                throw new Exception("قيمة السلفة غير صحيحة");

            var balance = await GetBalanceFromView(loan.cash_box_id);

            if (loan.loan_amount > balance)
                throw new Exception("رصيد الخزنة غير كاف");

            loan.loan_date = DateTime.SpecifyKind(
                loan.loan_date,
                DateTimeKind.Utc);

            loan.repaid_amount = 0;

            UpdateLoanStatus(loan);

            _context.employee_loans.Add(loan);
            await _context.SaveChangesAsync();

            var emp = await _context.employees
                .AsNoTracking()
                .FirstAsync(x => x.employee_id == loan.employee_id);

            AddFinancialEvent(
                "صرف سلفة",
                "OUT",
                loan.loan_amount,
                loan.cash_box_id,
                adminId,
                loan.loan_id,
                "employee_loans",
                loan.employee_id,
                emp.full_name,
                "صرف سلفة لموظف"
            );

            await _context.SaveChangesAsync();
        }

        public async Task<bool> AddLoanPayment(int loanId, decimal amount, int adminId)
        {
            var loan = await _context.employee_loans
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.loan_id == loanId);

            if (loan == null) return false;

            var remaining = loan.loan_amount - loan.repaid_amount;

            if (amount <= 0 || amount > remaining)
                return false;

            loan.repaid_amount += amount;

            UpdateLoanStatus(loan);

            AddFinancialEvent(
                "سداد سلفة",
                "IN",
                amount,
                loan.cash_box_id,
                adminId,
                loan.loan_id,
                "employee_loans",
                loan.employee_id,
                loan.Employee.full_name,
                "سداد يدوي لسلفة"
            );

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReverseLoanPayment(int loanId, decimal amount, int adminId)
        {
            var loan = await _context.employee_loans
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.loan_id == loanId);

            if (loan == null) return false;

            if (amount <= 0 || amount > loan.repaid_amount)
                return false;

            var balance = await GetBalanceFromView(loan.cash_box_id);

            if (amount > balance)
                return false;

            loan.repaid_amount -= amount;

            UpdateLoanStatus(loan);

            AddFinancialEvent(
                "إلغاء سداد سلفة",
                "OUT",
                amount,
                loan.cash_box_id,
                adminId,
                loan.loan_id,
                "employee_loans",
                loan.employee_id,
                loan.Employee.full_name,
                "إلغاء سداد سابق"
            );

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateLoan(EmployeeLoan updated, int adminId)
        {
            var loan = await _context.employee_loans
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.loan_id == updated.loan_id);

            if (loan == null)
                return false;

            if (updated.employee_id <= 0)
                throw new Exception("اختر الموظف");

            if (updated.cash_box_id <= 0)
                throw new Exception("اختر الخزنة");

            if (updated.loan_amount <= 0)
                throw new Exception("قيمة السلفة غير صحيحة");

            var oldData = new Dictionary<string, object>();
            var newData = new Dictionary<string, object>();

            // =====================================
            // تغيير الموظف
            // =====================================
            if (loan.employee_id != updated.employee_id)
            {
                var oldEmp = await _context.employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.employee_id == loan.employee_id);

                var newEmp = await _context.employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.employee_id == updated.employee_id);

                oldData["employee"] = oldEmp?.full_name ?? "";
                newData["employee"] = newEmp?.full_name ?? "";

                // تحديث كل الحركات المرتبطة بهذه السلفة
                var events = await _context.financial_events
                    .Where(x =>
                        x.ref_table == "employee_loans" &&
                        x.ref_id == loan.loan_id)
                    .ToListAsync();

                foreach (var ev in events)
                {
                    ev.person_id = updated.employee_id;
                    ev.person_name_snapshot = newEmp?.full_name;

                    var note = $"تم نقل السلفة من {oldEmp?.full_name} إلى {newEmp?.full_name}";

                    if (string.IsNullOrWhiteSpace(ev.notes))
                        ev.notes = note;
                    else if (!ev.notes.Contains(note))
                        ev.notes += " | " + note;
                }
            }

            // =====================================
            // تغيير الخزنة
            // =====================================
            if (loan.cash_box_id != updated.cash_box_id)
            {
                oldData["cash_box_id"] = loan.cash_box_id;
                newData["cash_box_id"] = updated.cash_box_id;

                var remaining = loan.loan_amount - loan.repaid_amount;

                var balance = await GetBalanceFromView(updated.cash_box_id);

                if (remaining > balance)
                    throw new Exception("رصيد الخزنة الجديدة غير كاف");

                var empName = await _context.employees
                    .Where(x => x.employee_id == updated.employee_id)
                    .Select(x => x.full_name)
                    .FirstOrDefaultAsync();

                AddFinancialEvent(
                    "نقل سلفة",
                    "IN",
                    remaining,
                    loan.cash_box_id,
                    adminId,
                    loan.loan_id,
                    "employee_loans",
                    updated.employee_id,
                    empName,
                    "إرجاع السلفة من الخزنة القديمة"
                );

                AddFinancialEvent(
                    "نقل سلفة",
                    "OUT",
                    remaining,
                    updated.cash_box_id,
                    adminId,
                    loan.loan_id,
                    "employee_loans",
                    updated.employee_id,
                    empName,
                    "صرف السلفة من الخزنة الجديدة"
                );
            }

            // =====================================
            // تعديل القيمة
            // =====================================
            if (loan.loan_amount != updated.loan_amount)
            {
                oldData["loan_amount"] = loan.loan_amount;
                newData["loan_amount"] = updated.loan_amount;

                if (updated.loan_amount < loan.repaid_amount)
                    throw new Exception("القيمة الجديدة أقل من المسدد");

                var empName = await _context.employees
                    .Where(x => x.employee_id == updated.employee_id)
                    .Select(x => x.full_name)
                    .FirstOrDefaultAsync();

                if (updated.loan_amount > loan.loan_amount)
                {
                    var diff = updated.loan_amount - loan.loan_amount;

                    var balance = await GetBalanceFromView(updated.cash_box_id);

                    if (diff > balance)
                        throw new Exception("رصيد الخزنة غير كاف");

                    AddFinancialEvent(
                        "زيادة سلفة",
                        "OUT",
                        diff,
                        updated.cash_box_id,
                        adminId,
                        loan.loan_id,
                        "employee_loans",
                        updated.employee_id,
                        empName,
                        "زيادة قيمة السلفة"
                    );
                }
                else
                {
                    var diff = loan.loan_amount - updated.loan_amount;

                    AddFinancialEvent(
                        "تخفيض سلفة",
                        "IN",
                        diff,
                        updated.cash_box_id,
                        adminId,
                        loan.loan_id,
                        "employee_loans",
                        updated.employee_id,
                        empName,
                        "تخفيض قيمة السلفة"
                    );
                }
            }

            // =====================================
            // تعديل التاريخ
            // =====================================
            if (loan.loan_date.Date != updated.loan_date.Date)
            {
                oldData["loan_date"] = loan.loan_date;
                newData["loan_date"] = updated.loan_date;
            }

            // =====================================
            // تعديل الملاحظات
            // =====================================
            if ((loan.notes ?? "") != (updated.notes ?? ""))
            {
                oldData["notes"] = loan.notes ?? "";
                newData["notes"] = updated.notes ?? "";
            }



            // =====================================
            // Audit Log
            // =====================================
            if (oldData.Count > 0)
            {
                AddAudit(
                    "employee_loans",
                    "UPDATE",
                    loan.loan_id.ToString(),
                    oldData,
                    newData,
                    adminId
                );
            }

            // =====================================
            // تحديث سجل السلفة
            // =====================================
            loan.employee_id = updated.employee_id;
            loan.cash_box_id = updated.cash_box_id;
            loan.loan_amount = updated.loan_amount;
            loan.notes = updated.notes;
            loan.loan_date = DateTime.SpecifyKind(
                updated.loan_date,
                DateTimeKind.Utc);

            UpdateLoanStatus(loan);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<decimal> GetEmployeeSalaryRemaining(int employeeId)
        {
            return await _context.salaries
                .Where(x =>
                    x.employee_id == employeeId &&
                    x.status != "خالص")
                .Select(x => x.amount - x.paid_amount)
                .SumAsync();
        }

        public async Task<bool> DeleteLoan(int loanId, int adminId)
        {
            var loan = await _context.employee_loans
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.loan_id == loanId);

            if (loan == null) return false;

            var remaining = loan.loan_amount - loan.repaid_amount;

            if (remaining > 0)
            {
                AddFinancialEvent(
                    "إلغاء سلفة",
                    "IN",
                    remaining,
                    loan.cash_box_id,
                    adminId,
                    loan.loan_id,
                    "employee_loans",
                    loan.employee_id,
                    loan.Employee.full_name,
                    "إلغاء سجل السلفة"
                );
            }

            AddAudit(
                "employee_loans",
                "DELETE",
                loan.loan_id.ToString(),
                new
                {
                    loan.employee_id,
                    loan.loan_amount,
                    loan.repaid_amount,
                    loan.status,
                    loan.cash_box_id
                },
                null,
                adminId
            );

            _context.employee_loans.Remove(loan);

            await _context.SaveChangesAsync();
            return true;
        }


        // =========================================
        // 🔹 purchases (المشتريات)
        // =========================================

        // جلب جميع المشتريات
        public async Task<List<Purchase>> GetPurchases()
        {
            return await _context.purchases
                .AsNoTracking()
                .OrderByDescending(x => x.purchase_date)
                .ToListAsync();
        }

        // حساب إجمالي الشراء
        private decimal GetPurchaseTotal(Purchase p)
        {
            return (p.quantity * p.unit_price)
                + p.customs_cost
                + p.shipping_cost
                + p.local_transport_cost;
        }

        // إضافة شراء جديد
        public async Task AddPurchase(Purchase item, int adminId)
        {
            item.notes = item.notes?.Trim();
            item.purchase_date = DateTime.SpecifyKind(item.purchase_date, DateTimeKind.Utc);

            if (item.quantity <= 0)
                throw new Exception("الكمية يجب أن تكون أكبر من صفر");

            if (item.unit_price <= 0)
                throw new Exception("سعر الوحدة يجب أن يكون أكبر من صفر");

            if (item.cash_box_id == null || item.cash_box_id <= 0)
                throw new Exception("اختر الخزنة");

            decimal total = GetPurchaseTotal(item);

            var balance = await GetBalanceFromView(item.cash_box_id.Value);

            if (total > balance)
                throw new Exception("رصيد الخزنة غير كافٍ");

            _context.purchases.Add(item);
            await _context.SaveChangesAsync();

            var supplier = await _context.suppliers
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.supplier_id == item.supplier_id);

            var material = await _context.raw_materials
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.raw_material_id == item.raw_material_id);

            AddFinancialEvent(
                "صرف شراء جديد",
                "OUT",
                total,
                item.cash_box_id.Value,
                adminId,
                item.purchase_id,
                "purchases",
                item.supplier_id,
                supplier?.name,
                "شراء مادة خام",
                item.raw_material_id,
                material?.name
            );

            await _context.SaveChangesAsync();
        }

        // تعديل شراء
        public async Task<bool> UpdatePurchase(Purchase updated, int adminId)
        {
            var p = await _context.purchases
                .FirstOrDefaultAsync(x => x.purchase_id == updated.purchase_id);

            if (p == null)
                return false;

            decimal oldTotal = GetPurchaseTotal(p);
            decimal newTotal = GetPurchaseTotal(updated);
            decimal diff = newTotal - oldTotal;

            updated.purchase_date = DateTime.SpecifyKind(
                updated.purchase_date,
                DateTimeKind.Utc);

            var supplier = await _context.suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.supplier_id == updated.supplier_id);

            var material = await _context.raw_materials
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.raw_material_id == updated.raw_material_id);

            // تغيير الخزنة
            if (p.cash_box_id != updated.cash_box_id &&
                p.cash_box_id != null &&
                updated.cash_box_id != null)
            {
                var balance = await GetBalanceFromView(updated.cash_box_id.Value);

                if (newTotal > balance)
                    throw new Exception("رصيد الخزنة الجديدة غير كافٍ");

                AddFinancialEvent(
                    "نقل مبلغ الشراء من الخزنة القديمة",
                    "IN",
                    oldTotal,
                    p.cash_box_id.Value,
                    adminId,
                    p.purchase_id,
                    "purchases",
                    updated.supplier_id,
                    supplier?.name,
                    "نقل تكلفة شراء",
                    updated.raw_material_id,
                    material?.name
                );

                AddFinancialEvent(
                    "نقل مبلغ الشراء إلى الخزنة الجديدة",
                    "OUT",
                    newTotal,
                    updated.cash_box_id.Value,
                    adminId,
                    p.purchase_id,
                    "purchases",
                    updated.supplier_id,
                    supplier?.name,
                    "نقل تكلفة شراء",
                    updated.raw_material_id,
                    material?.name
                );
            }
            else if (diff > 0 && p.cash_box_id != null)
            {
                var balance = await GetBalanceFromView(p.cash_box_id.Value);

                if (diff > balance)
                    return false;

                AddFinancialEvent(
                    "زيادة قيمة شراء بعد تعديل",
                    "OUT",
                    diff,
                    p.cash_box_id.Value,
                    adminId,
                    p.purchase_id,
                    "purchases",
                    updated.supplier_id,
                    supplier?.name,
                    "زيادة تكلفة شراء",
                    updated.raw_material_id,
                    material?.name
                );
            }
            else if (diff < 0 && p.cash_box_id != null)
            {
                AddFinancialEvent(
                    "استرجاع فرق شراء بعد تعديل",
                    "IN",
                    Math.Abs(diff),
                    p.cash_box_id.Value,
                    adminId,
                    p.purchase_id,
                    "purchases",
                    updated.supplier_id,
                    supplier?.name,
                    "استرجاع فرق شراء",
                    updated.raw_material_id,
                    material?.name
                );
            }

            // تحديث snapshot للحركات السابقة
            var events = await _context.financial_events
                .Where(x => x.ref_table == "purchases" && x.ref_id == p.purchase_id)
                .ToListAsync();

            foreach (var ev in events)
            {
                ev.person_id = updated.supplier_id;
                ev.item_id = updated.raw_material_id;
            }

            p.raw_material_id = updated.raw_material_id;
            p.supplier_id = updated.supplier_id;
            p.quantity = updated.quantity;
            p.unit_price = updated.unit_price;
            p.customs_cost = updated.customs_cost;
            p.local_transport_cost = updated.local_transport_cost;
            p.shipping_cost = updated.shipping_cost;
            p.purchase_date = updated.purchase_date;
            p.arrival_date = updated.arrival_date;
            p.cash_box_id = updated.cash_box_id;
            p.notes = updated.notes?.Trim();
            p.purchase_type = updated.purchase_type;

            await _context.SaveChangesAsync();
            return true;
        }

        // حذف شراء
        public async Task<bool> DeletePurchase(int id, int adminId)
        {
            var p = await _context.purchases
                .FirstOrDefaultAsync(x => x.purchase_id == id);

            if (p == null)
                return false;

            var supplier = await _context.suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.supplier_id == p.supplier_id);

            var material = await _context.raw_materials
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.raw_material_id == p.raw_material_id);

            if (p.cash_box_id != null)
            {
                decimal total = GetPurchaseTotal(p);

                AddFinancialEvent(
                    "حذف شراء وإرجاع المبلغ",
                    "IN",
                    total,
                    p.cash_box_id.Value,
                    adminId,
                    p.purchase_id,
                    "purchases",
                    p.supplier_id,
                    supplier?.name,
                    "حذف شراء وإرجاع القيمة للخزنة",
                    p.raw_material_id,
                    material?.name
                );
            }

            _context.purchases.Remove(p);

            await _context.SaveChangesAsync();
            return true;
        }


        public async Task<List<Supplier>> GetSuppliers()
        {
            return await _context.suppliers
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<RawMaterial>> GetRawMaterials()
        {
            return await _context.raw_materials .AsNoTracking()
                .AsNoTracking()
                .ToListAsync();
        }

    }

}
