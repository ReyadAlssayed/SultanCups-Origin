using SultanCups.Models;
using System.ComponentModel.DataAnnotations;

public class Order
{
    [Key]
    public int order_id { get; set; }

    public int person_id { get; set; }
    public string person_type { get; set; } = "";

    public decimal discount_total { get; set; }

    public decimal commission_per_box { get; set; }
    public bool commission_paid { get; set; }

    public DateTime order_date { get; set; }

    // 🔥 الجديد
    public int cash_box_id { get; set; } // الخزنة
    public string payment_method { get; set; } = "cash";

    // 🔥 الجديد
    public decimal paid_amount { get; set; } = 0;

    public string? notes { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}