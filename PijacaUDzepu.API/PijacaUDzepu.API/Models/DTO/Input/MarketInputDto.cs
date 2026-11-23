using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class MarketInputDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    // Centar pijace na mapi (opciono)
    [Range(-90, 90, ErrorMessage = "Lokacija nije ispravna.")]
    public double? Latitude { get; set; }
    [Range(-180, 180, ErrorMessage = "Lokacija nije ispravna.")]
    public double? Longitude { get; set; }
}
