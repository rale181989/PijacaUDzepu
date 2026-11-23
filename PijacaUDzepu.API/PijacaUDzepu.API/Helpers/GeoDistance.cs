namespace PijacaUDzepu.API.Helpers;

public static class GeoDistance
{
    // Udaljenost dve tačke u metrima (haversine), dovoljno tačno za proveru da je tezga na pijaci
    public static double Meters(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6_371_000;
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }
}
