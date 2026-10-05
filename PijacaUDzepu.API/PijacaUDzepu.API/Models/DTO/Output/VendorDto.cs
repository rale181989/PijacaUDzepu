namespace PijacaUDzepu.API.Models.DTO.Output;

public class VendorDto
{
    public int Id { get; set; }
    public int MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public int? StallId { get; set; }
    public string? StallLabel { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VendorShortDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
