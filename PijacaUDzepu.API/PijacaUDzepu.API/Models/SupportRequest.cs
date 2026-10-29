namespace PijacaUDzepu.API.Models;

// Pitanje ili prijava problema sa aplikacijom; stiže SuperAdmin-u u panel i na e-mail podrške
public class SupportRequest
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // Podaci o uređaju koji pomažu pri rešavanju problema
    public string? Platform { get; set; }
    public string? UserAgent { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
