using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class ProductInputDto
{
    [Required]
    [MaxLength(100, ErrorMessage = "Naziv proizvoda može imati najviše 100 znakova.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 999999)]
    public decimal Price { get; set; }

    [Required]
    public ProductUnit Unit { get; set; }

    [Required]
    public ProductCategory Category { get; set; }

    public ProductClass? ProductClass { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public bool IsAvailable { get; set; } = true;

    [Range(0, 1_000_000)]
    public decimal? StockQuantity { get; set; }
}

// Brza izmena sa liste proizvoda: samo popunjena polja se menjaju
public class QuickProductUpdateDto
{
    [Range(0.01, 999999)]
    public decimal? Price { get; set; }

    [Range(0, 1_000_000)]
    public decimal? StockQuantity { get; set; }

    // true = stanje se više ne prati
    public bool ClearStock { get; set; }

    public bool? IsAvailable { get; set; }
}
