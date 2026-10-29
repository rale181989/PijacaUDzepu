namespace PijacaUDzepu.API.Models;

// FCM token uređaja na koji se šalju push obaveštenja kada je aplikacija zatvorena
public class DeviceToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
