namespace PijacaUDzepu.API.Helpers;

// Funkcije koje mogu da se isključe bez novog koda (env Features__<Ime>=false). Klijent ih čita sa GET /api/features.
public static class Features
{
    // Lokacija tezge na mapi: prodavac na tezgi pritisne "Ovde sam", a tačku vide kurir i kupac koji je rezervisao robu.
    // Isključeno: endpoint za lokaciju vraća 400, DTO-i ne nose koordinate, a klijent ne prikazuje mapu.
    public static bool StallLocation { get; set; } = true;

    // Podloga mape (Leaflet tileLayer). OSM.de ispisuje i latinične nazive ulica (OSM standard je u Srbiji na ćirilici);
    // menja se bez novog APK-a, npr. na servis sa ključem (Maps__TileUrl, Maps__Attribution).
    public const string DefaultTileUrl = "https://tile.openstreetmap.de/{z}/{x}/{y}.png";
    public static string TileUrl { get; set; } = DefaultTileUrl;
    public static string TileAttribution { get; set; } = "&copy; OpenStreetMap";

    public static void Load(IConfiguration config)
    {
        StallLocation = config.GetValue("Features:StallLocation", true);
        TileUrl = config["Maps:TileUrl"] is { Length: > 0 } url ? url : DefaultTileUrl;
        TileAttribution = config["Maps:Attribution"] is { Length: > 0 } a ? a : "&copy; OpenStreetMap";
    }

    // Centar pijace mora biti u Srbiji (otprilike); zamenjene širina i dužina (19.8, 45.2) ispadaju iz okvira
    public static bool InSerbia(double lat, double lng) => lat is >= 41.8 and <= 46.2 && lng is >= 18.8 and <= 23.1;
}
