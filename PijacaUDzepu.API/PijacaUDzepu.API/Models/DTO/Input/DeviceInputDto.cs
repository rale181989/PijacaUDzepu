using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class RegisterDeviceDto
{
    [Required]
    [MaxLength(4096)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Platform { get; set; }
}

public class CourierShiftDto
{
    public bool IsOnShift { get; set; }
}
