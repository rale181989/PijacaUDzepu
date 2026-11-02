namespace PijacaUDzepu.API.Helpers;

// Pijace rade po beogradskom vremenu, a baza čuva UTC. Ako vremenska zona nije dostupna (npr. kontejner
// bez tzdata), koristi se UTC.
public static class LocalTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    public static DateTime Now => ToLocal(DateTime.UtcNow);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade"); }
        catch { return TimeZoneInfo.Utc; }
    }
}
