using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }

    // Opcioni odgovor kupcu uz promenu statusa
    [MaxLength(300)]
    public string? Message { get; set; }
}

// Kurir nije dostavio porudžbinu: razlog je obavezan
public class NotDeliveredDto
{
    [Required(ErrorMessage = "Upišite razlog zašto porudžbina nije dostavljena.")]
    [MaxLength(300)]
    public string Reason { get; set; } = string.Empty;
}
