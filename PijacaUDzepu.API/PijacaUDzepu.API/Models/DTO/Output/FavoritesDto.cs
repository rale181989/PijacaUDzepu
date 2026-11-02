namespace PijacaUDzepu.API.Models.DTO.Output;

public class FavoritesDto
{
    public List<int> ProductIds { get; set; } = new();
    public List<int> VendorIds { get; set; } = new();
    // Proizvodi za koje kupac čeka obaveštenje "kad bude dostupno"
    public List<int> AlertProductIds { get; set; } = new();
}

public class OrderRatingDto
{
    public int VendorScore { get; set; }
    public int? CourierScore { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
