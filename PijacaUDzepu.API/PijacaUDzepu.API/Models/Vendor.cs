namespace PijacaUDzepu.API.Models;

public class Vendor
{
    public int Id { get; set; }
    public int MarketId { get; set; }
    public int? StallId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Market Market { get; set; } = null!;
    public Stall? Stall { get; set; }
    public ICollection<VendorUser> VendorUsers { get; set; } = new List<VendorUser>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
