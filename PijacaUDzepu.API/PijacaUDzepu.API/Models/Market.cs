namespace PijacaUDzepu.API.Models;

public class Market
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    // Centar pijace na mapi (mapa tezgi i provera da je tačka tezge na pijaci); bez njega mapa kreće od centra grada
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
    public ICollection<Stall> Stalls { get; set; } = new List<Stall>();
}
