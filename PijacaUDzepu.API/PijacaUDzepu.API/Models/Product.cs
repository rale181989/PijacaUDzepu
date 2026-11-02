using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models;

public class Product
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductUnit Unit { get; set; }
    public ProductCategory Category { get; set; }
    public ProductClass? ProductClass { get; set; }
    public string? Note { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    // Količina na stanju (opciono; null = ne prati se). Porudžbina je umanjuje, a na nuli proizvod izlazi iz ponude.
    public decimal? StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
