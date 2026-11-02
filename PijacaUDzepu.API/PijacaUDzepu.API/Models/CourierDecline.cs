namespace PijacaUDzepu.API.Models;

// Kurir je odbio porudžbinu: ona mu se više ne prikazuje i ne zvoni mu, a ostali kuriri je i dalje vide
public class CourierDecline
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int CourierId { get; set; }
    public User Courier { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
