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
    public string? GuestEmail { get; set; }

    // Tajni kod iz linka za praćenje (/prati/{kod}); isti za sve porudžbine jedne gostujuće kupovine
    public string? TrackingCode { get; set; }

    // Kontakt i adresa za dostavu, i za goste i za prijavljene kupce
    public string? ContactPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public FulfillmentType Fulfillment { get; set; } = FulfillmentType.Courier;

    // Cena usluge kurira; kod rezervacije je 0. TotalAmount je samo vrednost robe.
    public decimal DeliveryFee { get; set; }

    // Odgovor prodavca kupcu (npr. "Spremno posle 10h")
    public string? VendorMessage { get; set; }
    public DateTime? VendorMessageAt { get; set; }

    public int? CourierId { get; set; }
    public User? Courier { get; set; }
    public DateTime? CourierAssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public bool IsGuestOrder => CustomerId == null;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
