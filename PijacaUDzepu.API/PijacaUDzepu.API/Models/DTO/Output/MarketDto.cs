namespace PijacaUDzepu.API.Models.DTO.Output;

public class MarketDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int VendorCount { get; set; }
}
