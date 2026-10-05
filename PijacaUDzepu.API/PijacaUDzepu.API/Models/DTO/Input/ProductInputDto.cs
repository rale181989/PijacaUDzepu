using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class ProductInputDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 999999)]
    public decimal Price { get; set; }

    [Required]
    public ProductUnit Unit { get; set; }

    public bool IsAvailable { get; set; } = true;
}
