using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class SupportRequestInputDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Problem|Question|Account|Other)$")]
    public string Category { get; set; } = "Problem";

    [Required]
    [MinLength(10)]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Platform { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    // Skriveno polje: ljudi ga ne vide, a botovi ga popune
    public string? Website { get; set; }
}
