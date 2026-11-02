namespace PijacaUDzepu.API.Models;

// Zahtev za novu lozinku ("Zaboravili ste lozinku?"). Čuva se samo SHA-256 hash tokena iz linka;
// token važi 2 sata i može se iskoristiti jednom. Sopstveni token umesto Identity-jevog jer bi se
// Identity-jev poništio pri svakom deploy-u na Render-u (ključevi za zaštitu podataka nisu trajni).
public class PasswordReset
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
