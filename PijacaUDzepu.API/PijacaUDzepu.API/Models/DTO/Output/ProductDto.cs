using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Output;

public class ProductDto
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string? StallLabel { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public ProductUnit Unit { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
}
