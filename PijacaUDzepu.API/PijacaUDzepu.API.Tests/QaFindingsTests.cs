using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PijacaUDzepu.API.Tests;

// Nalazi iz testiranja agentima (docs/testiranje/2026-11-10-agenti.md), grupa 1: oznake K1, V3... kao u izveštaju
[Collection("api")]
public class QaFindingsTests
{
    private readonly ApiFactory _factory;

    public QaFindingsTests(ApiFactory factory) => _factory = factory;

    private record Product(int Id, int VendorId, string Name, decimal Price, string Unit);
    private record Paged<T>(List<T> Items, int TotalCount);

    private async Task<Product> FindProduct(Api api, Func<Product, bool> match) =>
        (await api.Get<Paged<Product>>("/api/products?take=100")).Items.First(match);

    private static object Pickup(int productId, decimal quantity, decimal? expectedUnitPrice = null, string phone = "0641234567", string? note = null) => new
    {
        items = new[] { new { productId, quantity, expectedUnitPrice } },
        fulfillment = "Pickup",
        contactPhone = phone,
        note
    };

    // K1: cena se promenila posle dodavanja u korpu – porudžbina se ne prima, kupac dobija novu cenu
    [Fact]
    public async Task K1_order_is_refused_when_the_price_changed_since_the_cart()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);

        var stale = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1, product.Price + 1));
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.StartsWith($"Cena za {product.Name} je promenjena", await Api.Message(stale));

        var fresh = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1, product.Price));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    // V3: prodavac ne može da "otkaže" porudžbinu mimo odbijanja sa razlogom
    [Fact]
    public async Task V3_vendor_cannot_cancel_an_order()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("milan", "vendor1234");
        var product = await FindProduct(kupac, p => p.VendorId == 4);
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1))))[0];

        var res = await vendor.Send(HttpMethod.Put, $"/api/orders/{order.GetProperty("id").GetInt32()}/status", new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // V5 + V11: nepostojeća vrednost (broj umesto naziva) se odbija, uz poruku koju klijent prikazuje
    [Fact]
    public async Task V5_unknown_enum_values_are_rejected_with_a_message()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);
        var res = await kupac.Send(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } },
            fulfillment = 7,
            contactPhone = "0641234567"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Proverite unete podatke.", await Api.Message(res));

        var vendor = await _factory.As("ivana", "vendor1234");
        var badProduct = await vendor.Send(HttpMethod.Post, "/api/products", new { name = "Test", price = 10, unit = 99, category = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, badProduct.StatusCode);
        // Broj kao tekst ("7") takođe nije naziv
        var numericText = await kupac.Send(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } },
            fulfillment = "7",
            contactPhone = "0641234567"
        });
        Assert.Equal(HttpStatusCode.BadRequest, numericText.StatusCode);
    }

    // V5, nastavak: nepostojeća vrednost koja je već u bazi (stari red) ne sme da obori ceo spisak pri slanju
    [Fact]
    public async Task V5_an_unknown_enum_value_in_the_database_does_not_break_lists()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 9);
        await using var conn = new Npgsql.NpgsqlConnection(ApiFactory.ConnectionString);
        await conn.OpenAsync();
        async Task SetUnit(int unit)
        {
            await using var cmd = new Npgsql.NpgsqlCommand($"UPDATE \"Products\" SET \"Unit\" = {unit} WHERE \"Id\" = {product.Id}", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        await using var read = new Npgsql.NpgsqlCommand($"SELECT \"Unit\" FROM \"Products\" WHERE \"Id\" = {product.Id}", conn);
        var original = Convert.ToInt32(await read.ExecuteScalarAsync());

        await SetUnit(99);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await kupac.Send(HttpMethod.Get, "/api/products?take=100")).StatusCode);
        }
        finally
        {
            await SetUnit(original);
        }
    }

    // V6: negativno straničenje i ogromne količine ne ruše server (500)
    [Fact]
    public async Task V6_negative_paging_and_huge_amounts_do_not_crash()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        Assert.Equal(HttpStatusCode.OK, (await kupac.Send(HttpMethod.Get, "/api/orders/my-orders?skip=-1&take=-5")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await kupac.Send(HttpMethod.Get, "/api/products?skip=-5&take=-1")).StatusCode);

        var honey = await FindProduct(kupac, p => p.Name == "Domaći Med Lipov");
        var tooMany = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(honey.Id, 999999));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("Količina mora biti između 0,01 i 1000.", await Api.Message(tooMany));

        // 1000 kg meda po 1.200 din = 1,2 miliona: preko granice porudžbine
        var tooBig = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(honey.Id, 1000));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.StartsWith("Iznos porudžbine je prevelik", await Api.Message(tooBig));
    }

    // V7 + V11: dužina i format polja, poruka na srpskom (ili jeziku korisnika)
    [Fact]
    public async Task V7_lengths_and_formats_are_validated()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);

        var longNote = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1, note: new string('a', 501)));
        Assert.Equal("Napomena može imati najviše 500 znakova.", await Api.Message(longNote));

        var badPhone = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1, phone: "abc"));
        Assert.Equal(HttpStatusCode.BadRequest, badPhone.StatusCode);
        Assert.Equal("Unesite ispravan broj telefona.", await Api.Message(badPhone));

        var guest = await _factory.As();
        var noPhone = await guest.Send(HttpMethod.Post, "/api/orders/guest", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } },
            fulfillment = "Pickup", guestName = "Test"
        });
        Assert.Equal("Unesite broj telefona.", await Api.Message(noPhone));

        var reg = await guest.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName = "qa_v7_" + Guid.NewGuid().ToString("N")[..6], password = "lozinka123",
            firstName = new string('x', 51), lastName = "Test", phoneNumber = "0641234567", address = "Dunavska 5"
        });
        Assert.Equal("Ime može imati najviše 50 znakova.", await Api.Message(reg));
    }

    // V8: komad i veza samo u celim brojevima
    [Fact]
    public async Task V8_pieces_and_bunches_must_be_whole_numbers()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var piece = await FindProduct(kupac, p => p.Unit is "Komad" or "Veza");
        var res = await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(piece.Id, 0.5m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal($"{piece.Name}: količina mora biti ceo broj.", await Api.Message(res));
    }

    // V10: odgovor (i obaveštenje prodavcu) nosi ime prijavljenog kupca, ne "Gost"
    [Fact]
    public async Task V10_order_carries_the_customer_name()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1))))[0];
        Assert.Equal("Petar Petrović", order.GetProperty("customerName").GetString());
    }

    // V12: isti e-mail ne može na dva naloga
    [Fact]
    public async Task V12_registration_rejects_an_email_already_in_use()
    {
        var guest = await _factory.As();
        var res = await guest.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName = "qa_v12_" + Guid.NewGuid().ToString("N")[..6], password = "lozinka123",
            firstName = "Test", lastName = "Test", email = "PETAR@example.com", phoneNumber = "0641234567", address = "Dunavska 5"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Ovaj e-mail već koristi drugi nalog.", await Api.Message(res));
    }

    // P2: "Danas zatvaram u ..." zatvara tezgu samo danas; nedeljni raspored ostaje netaknut
    [Fact]
    public async Task P2_early_close_applies_only_today()
    {
        var vendor = await _factory.As("milan", "vendor1234");
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 4);
        object Availability(string? earlyClose) => new { ordersPaused = false, openingHours = Array.Empty<object>(), closedDates = Array.Empty<string>(), earlyCloseToday = earlyClose };

        try
        {
            // Vreme je već prošlo: tezga je zatvorena do sutra
            var closed = await Api.Read<JsonElement>(await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability", Availability("00:00")));
            Assert.False(closed.GetProperty("isOpen").GetBoolean());
            Assert.Equal("00:00", closed.GetProperty("earlyCloseToday").GetString());
            Assert.Equal(0, closed.GetProperty("openingHours").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1))).StatusCode);

            // Vreme je pred nama: radi, ali kupac vidi do kada
            var open = await Api.Read<JsonElement>(await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability", Availability("23:59")));
            Assert.True(open.GetProperty("isOpen").GetBoolean());
            Assert.Equal("23:59", open.GetProperty("closesAt").GetString());

            var bad = await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability", Availability("25:00"));
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        finally
        {
            (await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability", Availability(null))).EnsureSuccessStatusCode();
        }
    }

    // P6: "Pregled dana" – spremanje broji samo prihvaćeno, porudžbine na čekanju su posebno, spakovano se ne broji
    [Fact]
    public async Task P6_vendor_today_separates_accepted_from_awaiting()
    {
        var vendor = await _factory.As("nikola", "vendor1234");
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);
        decimal Qty(JsonElement today, string list) =>
            today.GetProperty(list).EnumerateArray().Where(x => x.GetProperty("productName").GetString() == product.Name)
                .Sum(x => x.GetProperty("quantity").GetDecimal());

        var before = await vendor.Get<JsonElement>("/api/orders/vendor-today");
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 2))))[0];
        var id = order.GetProperty("id").GetInt32();

        var pending = await vendor.Get<JsonElement>("/api/orders/vendor-today");
        Assert.Equal(Qty(before, "awaitingAnswer") + 2, Qty(pending, "awaitingAnswer"));
        Assert.Equal(Qty(before, "toPrepare"), Qty(pending, "toPrepare"));

        (await vendor.Send(HttpMethod.Put, $"/api/orders/{id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        var confirmed = await vendor.Get<JsonElement>("/api/orders/vendor-today");
        Assert.Equal(Qty(before, "toPrepare") + 2, Qty(confirmed, "toPrepare"));
        Assert.Equal(Qty(before, "awaitingAnswer"), Qty(confirmed, "awaitingAnswer"));

        (await vendor.Send(HttpMethod.Put, $"/api/orders/{id}/status", new { status = "ReadyForPickup" })).EnsureSuccessStatusCode();
        var ready = await vendor.Get<JsonElement>("/api/orders/vendor-today");
        Assert.Equal(Qty(before, "toPrepare"), Qty(ready, "toPrepare"));
    }

    // C4: pretraga razume jedninu i sitnu grešku na kraju reči
    [Theory]
    [InlineData("jabuka", "Jabuke Crveni Delišes")]
    [InlineData("paradaiz", "Paradajz Domaći")]
    [InlineData("jabuke", "Jabuke Crveni Delišes")]
    public async Task C4_search_finds_singular_and_small_typos(string term, string expected)
    {
        var guest = await _factory.As();
        var page = await guest.Get<Paged<Product>>($"/api/products?search={Uri.EscapeDataString(term)}&take=50");
        Assert.Contains(page.Items, p => p.Name == expected);
    }

    // C7: filter nudi samo grupe proizvoda u kojima ima robe
    [Fact]
    public async Task C7_only_categories_with_products_are_offered()
    {
        var guest = await _factory.As();
        var categories = await guest.Get<List<string>>("/api/products/categories");
        Assert.Contains("Voce", categories);
        Assert.DoesNotContain("Jaja", categories);
    }

    // C3: gostujuća porudžbina sa ovog uređaja prelazi na nalog posle registracije
    [Fact]
    public async Task C3_guest_orders_from_this_device_move_to_the_new_account()
    {
        var guest = await _factory.As();
        var product = await FindProduct(guest, p => p.VendorId == 3);
        var placed = (await Api.Read<List<JsonElement>>(await guest.Send(HttpMethod.Post, "/api/orders/guest", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } },
            fulfillment = "Pickup", guestName = "Novi Kupac", guestPhone = "0641234567"
        })))[0];
        var code = placed.GetProperty("trackingCode").GetString();

        var userName = "qa_c3_" + Guid.NewGuid().ToString("N")[..6];
        (await guest.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName, password = "lozinka123", firstName = "Novi", lastName = "Kupac", phoneNumber = "0641234567", address = "Dunavska 5"
        })).EnsureSuccessStatusCode();
        var customer = await _factory.As(userName, "lozinka123");

        var claim = await Api.Read<JsonElement>(await customer.Send(HttpMethod.Post, "/api/orders/claim", new { trackingCodes = new[] { code } }));
        Assert.Equal(1, claim.GetProperty("claimed").GetInt32());
        var mine = await customer.Get<Paged<JsonElement>>("/api/orders/my-orders");
        Assert.Contains(mine.Items, o => o.GetProperty("id").GetInt32() == placed.GetProperty("id").GetInt32());

        // Već preuzeta porudžbina se ne preuzima ponovo (ni na drugi nalog)
        var other = await _factory.As("kupac", "kupac1234");
        Assert.Equal(0, (await Api.Read<JsonElement>(await other.Send(HttpMethod.Post, "/api/orders/claim", new { trackingCodes = new[] { code } }))).GetProperty("claimed").GetInt32());
    }

    // C8: e-mail može da se doda u "Moj nalog"; zauzet e-mail se odbija, a izostavljen se ne briše
    [Fact]
    public async Task C8_email_can_be_set_in_the_profile()
    {
        var guest = await _factory.As();
        var userName = "qa_c8_" + Guid.NewGuid().ToString("N")[..6];
        (await guest.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName, password = "lozinka123", firstName = "Test", lastName = "Test", phoneNumber = "0641234567", address = "Dunavska 5"
        })).EnsureSuccessStatusCode();
        var user = await _factory.As(userName, "lozinka123");

        var taken = await user.Send(HttpMethod.Put, "/api/auth/profile", new { phoneNumber = "0641234567", address = "Dunavska 5", email = "petar@example.com" });
        Assert.Equal("Ovaj e-mail već koristi drugi nalog.", await Api.Message(taken));

        var email = $"{userName}@example.com";
        var ok = await Api.Read<JsonElement>(await user.Send(HttpMethod.Put, "/api/auth/profile", new { phoneNumber = "0641234567", address = "Dunavska 5", email }));
        Assert.Equal(email, ok.GetProperty("email").GetString());

        var kept = await Api.Read<JsonElement>(await user.Send(HttpMethod.Put, "/api/auth/profile", new { phoneNumber = "0641234567", address = "Dunavska 6" }));
        Assert.Equal(email, kept.GetProperty("email").GetString());
    }

    // D7: SuperAdmin otvara svaku porudžbinu
    [Fact]
    public async Task D7_super_admin_can_open_any_order()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = await FindProduct(kupac, p => p.VendorId == 3);
        var order = (await Api.Read<List<JsonElement>>(await kupac.Send(HttpMethod.Post, "/api/orders", Pickup(product.Id, 1))))[0];
        var admin = await _factory.As("admin", "admin1234");
        Assert.Equal(HttpStatusCode.OK, (await admin.Send(HttpMethod.Get, $"/api/orders/{order.GetProperty("id").GetInt32()}")).StatusCode);
    }

    // D9: ista granica cene svuda, tačna poruka za neispravno radno vreme, bez dva proizvoda istog naziva
    [Fact]
    public async Task D9_price_limit_hours_message_and_unique_product_names()
    {
        var vendor = await _factory.As("ivana", "vendor1234");
        var mine = await vendor.Get<List<JsonElement>>("/api/products/my-products");
        var first = mine[0];
        var id = first.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await vendor.Send(HttpMethod.Patch, $"/api/products/{id}/quick", new { price = 5_000_000 })).StatusCode);

        var hours = await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability", new
        {
            ordersPaused = false, openingHours = new[] { new { day = 9, from = "25:00", to = "26:00" } }, closedDates = Array.Empty<string>()
        });
        Assert.Equal("Radno vreme nije ispravno: dan ili vreme ne postoji (vreme kao 07:00).", await Api.Message(hours));

        var name = first.GetProperty("name").GetString()!;
        var dup = await vendor.Send(HttpMethod.Post, "/api/products", new { name = "  " + name.ToUpper() + " ", price = 100, unit = "Kg", category = "Voce", isAvailable = true });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Equal($"Već imate proizvod sa nazivom „{name.ToUpper()}”.", await Api.Message(dup));
    }

    // V4: lažna adresa na početku X-Forwarded-For ne zaobilazi ograničenje prijava (računa se poslednja)
    [Fact]
    public async Task V4_spoofed_forwarded_for_does_not_bypass_the_login_limit()
    {
        var http = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { userName = "nepostoji_v4", password = "pogresna123" })
            };
            req.Headers.Add("X-Forwarded-For", $"1.2.3.{i}, 10.251.0.1");
            statuses.Add((await http.SendAsync(req)).StatusCode);
        }
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
