namespace PijacaUDzepu.API.Models.DTO.Output;

public class StallDto
{
    public int Id { get; set; }
    public int MarketId { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
