using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SultanCups.Models
{
    public class EmployeeLoan
    {
        [Key]
        public int loan_id { get; set; }

        public int employee_id { get; set; }

        public int cash_box_id { get; set; }

        public decimal loan_amount { get; set; }

        public decimal repaid_amount { get; set; }

        public string status { get; set; } = null!;

        public DateTime loan_date { get; set; }

        public DateTime created_at { get; set; }
        public string? notes { get; set; }


        [ForeignKey("employee_id")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("cash_box_id")]
        public CashBox CashBox { get; set; } = null!;

        [NotMapped]
        public decimal Remaining => loan_amount - repaid_amount;
    }
}