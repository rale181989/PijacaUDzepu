namespace PijacaUDzepu.API.Models;

// Omiljeni proizvod ili prodavac kupca (tačno jedno od ProductId/VendorId je popunjeno)
public class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// "Obavesti me kad bude dostupno": kad prodavac vrati proizvod u ponudu, kupac dobija obaveštenje i zapis se briše
public class StockAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Ocena završene porudžbine (1–5): prodavac i, kod dostave, kurir. Jedna po porudžbini.
public class OrderRating
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int VendorScore { get; set; }
    public int? CourierScore { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
