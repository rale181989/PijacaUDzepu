using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Output;

public class CourierProfileDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public CourierVehicle? Vehicle { get; set; }
    public string? LicensePlate { get; set; }
    public bool IsOnShift { get; set; }
}
