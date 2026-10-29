using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models;

// Podaci kurira koje on sam unosi; ime, prezime i telefon su na User-u
public class CourierProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public CourierVehicle? Vehicle { get; set; }
    public string? LicensePlate { get; set; }

    // Obaveštenja o novim porudžbinama dobijaju samo kuriri na smeni
    public bool IsOnShift { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
