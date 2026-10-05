using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class StallInputDto
{
    [Required]
    public int MarketId { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;
}
