using Microsoft.Extensions.Caching.Memory;

namespace PijacaUDzepu.API.Services;

// Zaključavanje prijave posle 10 pogrešnih lozinki, ali samo za adresu sa koje su stigle.
// Ranije se zaključavao ceo nalog (Identity lockout), pa je bilo ko mogao da zaključa tuđi nalog, i admin.
// Ključ sadrži SecurityStamp, pa nova lozinka (zaboravljena lozinka, reset od admina) briše raniji brojač.
public class LoginThrottle
{
    public const int MaxFailures = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public LoginThrottle(IMemoryCache cache) => _cache = cache;

    private static string Key(int userId, string? securityStamp, string ip) => $"login-fail:{userId}:{securityStamp}:{ip}";

    public bool IsBlocked(int userId, string? securityStamp, string ip) =>
        _cache.TryGetValue(Key(userId, securityStamp, ip), out int failures) && failures >= MaxFailures;

    // Vraća true kada je ovim pokušajem prijava sa te adrese zaključana
    public bool RecordFailure(int userId, string? securityStamp, string ip)
    {
        var key = Key(userId, securityStamp, ip);
        var failures = _cache.TryGetValue(key, out int current) ? current + 1 : 1;
        _cache.Set(key, failures, Window);
        return failures >= MaxFailures;
    }

    public void Reset(int userId, string? securityStamp, string ip) => _cache.Remove(Key(userId, securityStamp, ip));
}
