namespace PijacaUDzepu.API.Helpers;

// Funkcije koje mogu da se isključe bez novog koda (env Features__<Ime>=false). Klijent ih čita sa GET /api/features.
public static class Features
{
    // Lokacija tezge na mapi: prodavac na tezgi pritisne "Ovde sam", a tačku vide kurir i kupac koji je rezervisao robu.
    // Isključeno: endpoint za lokaciju vraća 400, DTO-i ne nose koordinate, a klijent ne prikazuje mapu.
    public static bool StallLocation { get; set; } = true;

    public static void Load(IConfiguration config)
    {
        StallLocation = config.GetValue("Features:StallLocation", true);
    }
}
