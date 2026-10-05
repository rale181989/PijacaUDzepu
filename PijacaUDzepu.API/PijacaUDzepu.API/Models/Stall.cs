namespace PijacaUDzepu.API.Models;

public class Stall
{
    public int Id { get; set; }
    public int MarketId { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Market Market { get; set; } = null!;
    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
}
