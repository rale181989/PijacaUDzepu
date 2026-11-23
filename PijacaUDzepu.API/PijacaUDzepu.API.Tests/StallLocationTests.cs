using System.Net;
using System.Text.Json;

namespace PijacaUDzepu.API.Tests;

// Lokacija tezge na mapi (Features.StallLocation, grana feature/lokacija-tezge; nalazi LP/LK iz docs/testiranje/2026-11-23-agenti.md)
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
        var features = await anonymous.Get<JsonElement>("/api/features");
        Assert.True(features.GetProperty("stallLocation").GetBoolean());
        Assert.StartsWith("https://", features.GetProperty("mapTileUrl").GetString());

        // Marko je na Futoškoj pijaci (centar 45.2529, 19.8371)
        var marko = await _factory.As("marko", "vendor1234");
        var far = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.30, longitude = 19.90 });
        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);
        Assert.Contains("predaleko", await Api.Message(far));

        var bad = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 120, longitude = 19.83 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        // LP4: prazan zahtev se ne čuva kao 0,0
        Assert.Equal(HttpStatusCode.BadRequest, (await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { })).StatusCode);

        var ok = await marko.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.2531, longitude = 19.8368 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var vendor = await Api.Read<JsonElement>(ok);
        Assert.Equal(45.2531, vendor.GetProperty("stallLatitude").GetDouble(), 4);
        Assert.Equal(45.2529, vendor.GetProperty("marketLatitude").GetDouble(), 4);

        // Kupac sa rezervacijom vidi tačku tezge i centar pijace uz porudžbinu
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = (await kupac.Get<Paged<Product>>("/api/products?take=100")).Items.First(p => p.VendorId == 1);
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1m } },
            fulfillment = "Pickup",
            contactPhone = "0641234567"
        })))[0];
        Assert.Equal(19.8368, order.GetProperty("stallLongitude").GetDouble(), 4);
        Assert.Equal(19.8371, order.GetProperty("marketLongitude").GetDouble(), 4);
        (await marko.Send(HttpMethod.Put, $"/api/orders/{order.GetProperty("id").GetInt32()}/status",
            new { status = "Rejected", message = "Nestalo je robe" })).EnsureSuccessStatusCode();

        // LK9: kupac kurirske porudžbine ne dobija tačku tezge, a prodavac je dobija
        var courier = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1m } },
            fulfillment = "Courier",
            contactPhone = "0641234567",
            deliveryAddress = "Dunavska 5, Novi Sad"
        })))[0];
        var courierId = courier.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.Null, courier.GetProperty("stallLatitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await kupac.Get<JsonElement>($"/api/orders/{courierId}")).GetProperty("stallLatitude").ValueKind);
        Assert.Equal(45.2531, (await marko.Get<JsonElement>($"/api/orders/{courierId}")).GetProperty("stallLatitude").GetDouble(), 4);
        (await marko.Send(HttpMethod.Put, $"/api/orders/{courierId}/status",
            new { status = "Rejected", message = "Nestalo je robe" })).EnsureSuccessStatusCode();

        // Samo prodavac postavlja tačku; brisanje vraća stanje bez tačke
        Assert.Equal(HttpStatusCode.Forbidden,
            (await kupac.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 45.2531, longitude = 19.8368 })).StatusCode);
        var cleared = await Api.Read<JsonElement>(await marko.Send(HttpMethod.Delete, "/api/vendors/my-vendor/location"));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("stallLatitude").ValueKind);
    }

    // LP3: druga tezga ili pijaca u profilu briše staru tačku
    [Fact]
    public async Task Changing_the_stall_clears_the_point()
    {
        var bojan = await _factory.As("bojan", "vendor1234");
        var me = await bojan.Get<JsonElement>("/api/vendors/my-vendor");
        var lat = me.GetProperty("marketLatitude").GetDouble();
        var lng = me.GetProperty("marketLongitude").GetDouble();
        (await bojan.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = lat, longitude = lng })).EnsureSuccessStatusCode();

        object Profile(int? stallId) => new
        {
            marketId = me.GetProperty("marketId").GetInt32(),
            stallId,
            name = me.GetProperty("name").GetString(),
            description = me.GetProperty("description").GetString(),
            phones = me.GetProperty("phones").EnumerateArray().Select(p => p.GetString()).ToList()
        };
        var originalStall = me.GetProperty("stallId").ValueKind == JsonValueKind.Null ? (int?)null : me.GetProperty("stallId").GetInt32();

        // Isti profil (bez promene tezge) ne briše tačku
        var same = await Api.Read<JsonElement>(await bojan.Send(HttpMethod.Put, "/api/vendors/my-vendor", Profile(originalStall)));
        Assert.Equal(lat, same.GetProperty("stallLatitude").GetDouble(), 4);

        var moved = await Api.Read<JsonElement>(await bojan.Send(HttpMethod.Put, "/api/vendors/my-vendor", Profile(null)));
        Assert.Equal(JsonValueKind.Null, moved.GetProperty("stallLatitude").ValueKind);
        (await bojan.Send(HttpMethod.Put, "/api/vendors/my-vendor", Profile(originalStall))).EnsureSuccessStatusCode();
    }

    // LP4, LP5: pijaca bez centra ne prima tačku; centar mora biti u Srbiji (zamenjene koordinate se odbijaju)
    [Fact]
    public async Task Market_centre_is_required_and_must_be_in_Serbia()
    {
        var admin = await _factory.As("admin", "admin1234");
        var ana = await _factory.As("ana", "vendor1234");
        var me = await ana.Get<JsonElement>("/api/vendors/my-vendor");
        var marketId = me.GetProperty("marketId").GetInt32();
        var market = (await admin.Get<List<JsonElement>>("/api/markets/all")).Single(m => m.GetProperty("id").GetInt32() == marketId);
        var name = market.GetProperty("name").GetString();
        var address = market.GetProperty("address").GetString();
        var lat = market.GetProperty("latitude").GetDouble();
        var lng = market.GetProperty("longitude").GetDouble();

        var swapped = await admin.Send(HttpMethod.Put, $"/api/markets/{marketId}", new { name, address, latitude = lng, longitude = lat });
        Assert.Equal(HttpStatusCode.BadRequest, swapped.StatusCode);
        Assert.Contains("zamenjene", await Api.Message(swapped));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.Send(HttpMethod.Put, $"/api/markets/{marketId}", new { name, address, latitude = lat })).StatusCode);

        (await admin.Send(HttpMethod.Put, $"/api/markets/{marketId}", new { name, address })).EnsureSuccessStatusCode();
        try
        {
            var noCentre = await ana.Send(HttpMethod.Put, "/api/vendors/my-vendor/location", new { latitude = 44.81, longitude = 20.46 });
            Assert.Equal(HttpStatusCode.BadRequest, noCentre.StatusCode);
            Assert.Contains("centar", await Api.Message(noCentre));
        }
        finally
        {
            (await admin.Send(HttpMethod.Put, $"/api/markets/{marketId}", new { name, address, latitude = lat, longitude = lng })).EnsureSuccessStatusCode();
        }
    }
}
