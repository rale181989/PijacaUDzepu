using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class VendorInputDto
{
    [Required]
    public int MarketId { get; set; }

    public int? StallId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    public bool AcceptsReservations { get; set; }
    public bool OffersDelivery { get; set; }
    [Range(0, 1000000)]
    public decimal? MinOrderAmount { get; set; }
    [Range(1, 500)]
    public int? DeliveryRadiusKm { get; set; }
    public List<DeliveryScheduleEntry>? DeliverySchedule { get; set; }
}
