using System.Net;
using System.Text.Json;

namespace PijacaUDzepu.API.Tests;

// Lokacija tezge na mapi (Features.StallLocation, grana feature/lokacija-tezge)
[Collection("api")]
public class StallLocationTests
{
    private readonly ApiFactory _factory;

    public StallLocationTests(ApiFactory factory) => _factory = factory;

    private record Product(int Id, int VendorId);
    private record Paged<T>(List<T> Items);

    [Fact]
    public async Task Vendor_sets_the_stall_point_and_the_customer_with_a_reservation_sees_it()
    {
        var anonymous = await _factory.As();
        Assert.True((await anonymous.Get<JsonElement>("/api/features")).GetProperty("stallLocation").GetBoolean());

        // Marko je na Futoškoj pijaci (demo centar 45.2522, 19.8306)
        var marko = await _factory.As("marko", "vendor1234");
        var far = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.30, longitude = 19.90 });
        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);
        Assert.Contains("predaleko", await Api.Message(far));

        var bad = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 120, longitude = 19.83 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.2524, longitude = 19.8311 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var vendor = await Api.Read<JsonElement>(ok);
        Assert.Equal(45.2524, vendor.GetProperty("stallLatitude").GetDouble(), 4);
        Assert.Equal(45.2522, vendor.GetProperty("marketLatitude").GetDouble(), 4);

        // Kupac sa rezervacijom vidi tačku tezge i centar pijace uz porudžbinu
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = (await kupac.Get<Paged<Product>>("/api/products?take=100")).Items.First(p => p.VendorId == 1);
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1m } },
            fulfillment = "Pickup",
            contactPhone = "0641234567"
        })))[0];
        Assert.Equal(19.8311, order.GetProperty("stallLongitude").GetDouble(), 4);
        Assert.Equal(19.8306, order.GetProperty("marketLongitude").GetDouble(), 4);
        (await marko.Send(HttpMethod.Put, $"/api/orders/{order.GetProperty("id").GetInt32()}/status",
            new { status = "Rejected", message = "Nestalo je robe" })).EnsureSuccessStatusCode();

        // Samo prodavac postavlja tačku; brisanje vraća stanje bez tačke
        Assert.Equal(HttpStatusCode.Forbidden,
            (await kupac.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.2524, longitude = 19.8311 })).StatusCode);
        var cleared = await Api.Read<JsonElement>(await marko.Send(HttpMethod.Delete, "/api/vendors/my-vendor/location"));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("stallLatitude").ValueKind);
    }
}
