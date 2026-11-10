namespace PijacaUDzepu.API.Helpers;

public static class RequestIp
{
    // Render dodaje pravu adresu klijenta na KRAJ X-Forwarded-For; prve vrednosti može da upiše sam klijent,
    // pa bi se njima zaobilazila ograničenja (broj prijava, forma za podršku, zaključavanje prijave)
    public static string Of(HttpContext context)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].ToString().Split(',').LastOrDefault()?.Trim();
        return string.IsNullOrEmpty(ip) ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : ip;
    }
}
