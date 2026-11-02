using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class FavoritesSyncDto
{
    public List<int> ProductIds { get; set; } = new();
    public List<int> VendorIds { get; set; } = new();
}

public class OrderRatingInputDto
{
    [Range(1, 5)]
    public int VendorScore { get; set; }

    [Range(1, 5)]
    public int? CourierScore { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}
