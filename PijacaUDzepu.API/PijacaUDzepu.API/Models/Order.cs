using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models;

public class Order
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public User? Customer { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestAddress { get; set; }

    public bool IsGuestOrder => CustomerId == null;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
