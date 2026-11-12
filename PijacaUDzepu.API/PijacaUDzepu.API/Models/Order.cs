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
    // Sve porudžbine jedne korpe (po jedna za svakog prodavca) imaju isti CheckoutId - kurir ih vozi zajedno
    public Guid? CheckoutId { get; set; }
    // Kada je prodavac prvi put odgovorio (prešla iz "na čekanju") - za vreme odgovora u izveštaju
    public DateTime? RespondedAt { get; set; }
    // Kada je poslato upozorenje da porudžbina predugo čeka (OrderWatchdog), da se ne šalje ponovo
    public DateTime? WaitAlertSentAt { get; set; }
    // Kada su kuriri dobili porudžbinu: tek kada se izjasne svi prodavci iz iste korpe (nijedna nije na čekanju)
    public DateTime? CourierOfferedAt { get; set; }

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

    // Kurir nije dostavio: razlog i vreme
    public string? NotDeliveredReason { get; set; }
    public DateTime? NotDeliveredAt { get; set; }

    // Trag kada SuperAdmin ručno zatvori porudžbinu koja je ostala otvorena
    public int? ClosedByAdminId { get; set; }
    public User? ClosedByAdmin { get; set; }
    public DateTime? ClosedByAdminAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public bool IsGuestOrder => CustomerId == null;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public OrderRating? Rating { get; set; }
}
