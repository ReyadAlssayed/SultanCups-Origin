using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SultanCups.Models
{
    public class Salary
    {
        [Key]
        public int salary_id { get; set; }

        public int employee_id { get; set; }

        public string salary_type { get; set; } = null!;

        public decimal amount { get; set; }

        public decimal paid_amount { get; set; }

        public string status { get; set; } = null!;
        
        public DateTime salary_date { get; set; }

        public DateTime created_at { get; set; }
        public int cash_box_id { get; set; }

        public string? notes { get; set; }

        [ForeignKey("employee_id")]
        public Employee Employee { get; set; } = null!;

        [ForeignKey("cash_box_id")]
        public CashBox CashBox { get; set; } = null!;

        [NotMapped]
        public decimal Remaining => amount - paid_amount;
    }
}