namespace PijacaUDzepu.API.Helpers;

// Poznate adrese klijenta (lokalne i Render) rade paralelno, bez menjanja konfiguracije.
// Linkovi u e-mailovima vode na adresu sa koje je zahtev stigao, ako je poznata;
// inače (npr. mobilna aplikacija, čiji je origin https://localhost) na javni sajt.
public class FrontendUrlResolver
{
    public const string PublicUrl = "https://pijacaudzepu.onrender.com";

    public static readonly string[] DefaultFrontendUrls =
    {
        "http://localhost:4200",
        "http://localhost:8100",
        PublicUrl
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;

    public FrontendUrlResolver(IHttpContextAccessor httpContextAccessor, IConfiguration config)
    {
        _httpContextAccessor = httpContextAccessor;
        _config = config;
    }

    public static string[] KnownUrls(IConfiguration config) =>
        (config["FrontendUrls"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? config.GetSection("FrontendUrls").Get<string[]>()
            ?? DefaultFrontendUrls)
        .Select(u => u.TrimEnd('/'))
        .ToArray();

    // Mora se pozvati dok traje HTTP zahtev (ne iz pozadinskog zadatka)
    public string Resolve()
    {
        var fallback = (_config["FrontendUrl"] ?? PublicUrl).TrimEnd('/');
        var origin = _httpContextAccessor.HttpContext?.Request.Headers.Origin.ToString().TrimEnd('/');
        if (string.IsNullOrEmpty(origin)) return fallback;

        return KnownUrls(_config).Contains(origin, StringComparer.OrdinalIgnoreCase) ? origin : fallback;
    }
}
