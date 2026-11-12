using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PijacaUDzepu.API.Services;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Tests;

// Najosetljiviji tokovi porudžbina: podela korpe i iznosi, trka kurira, stanje, zatvorena tezga, odbijanje
[Collection("api")]
public class OrderFlowTests
{
    private readonly ApiFactory _factory;

    public OrderFlowTests(ApiFactory factory) => _factory = factory;

    private record Product(int Id, int VendorId, string Name, decimal Price, bool IsAvailable, decimal? StockQuantity);
    private record Paged<T>(List<T> Items, int TotalCount);
    private record Order(int Id, int VendorId, string Status, decimal TotalAmount, decimal DeliveryFee, decimal AmountToPay, Guid? CheckoutId);

    private async Task<List<Product>> VendorProducts(Api api, int vendorId) =>
        (await api.Get<Paged<Product>>($"/api/products?vendorId={vendorId}&take=50")).Items;

    private static object CourierOrder(params (int productId, decimal quantity)[] items) => new
    {
        items = items.Select(i => new { productId = i.productId, quantity = i.quantity }),
        fulfillment = "Courier",
        contactPhone = "0641234567",
        deliveryAddress = "Bulevar oslobođenja 12, Novi Sad"
    };

    [Fact]
    public async Task Cart_with_two_vendors_is_split_into_orders_with_fee_per_vendor()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var marko = (await VendorProducts(kupac, 1))[0];
        var jovana = (await VendorProducts(kupac, 2))[0];
        var fee = (await kupac.Get<JsonElement>("/api/orders/delivery-options")).GetProperty("courierFee").GetDecimal();

        var res = await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((marko.Id, 2), (jovana.Id, 1)));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var orders = await Api.Read<List<Order>>(res);

        Assert.Equal(2, orders.Count);
        Assert.Equal(orders[0].CheckoutId, orders[1].CheckoutId);
        var forMarko = orders.Single(o => o.VendorId == 1);
        Assert.Equal(marko.Price * 2, forMarko.TotalAmount);
        Assert.Equal(fee, forMarko.DeliveryFee);
        Assert.Equal(forMarko.TotalAmount + fee, forMarko.AmountToPay);
    }

    [Fact]
    public async Task Only_the_first_courier_gets_the_order()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var kurir1 = await _factory.As("kurir1", "kurir1234");
        var kurir2 = await _factory.As("kurir2", "kurir1234");
        foreach (var k in new[] { kurir1, kurir2 })
            (await k.Send(HttpMethod.Put, "/api/couriers/me/shift", new { isOnShift = true })).EnsureSuccessStatusCode();

        var product = (await VendorProducts(kupac, 3))[0];
        var order = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];
        var vendor = await _factory.As("nikola", "vendor1234");
        (await vendor.Send(HttpMethod.Put, $"/api/orders/{order.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();

        var results = await Task.WhenAll(
            kurir1.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/accept"),
            kurir2.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/accept"));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stock_cannot_be_oversold_by_concurrent_orders_and_comes_back_on_reject()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("nikola", "vendor1234");
        var product = (await VendorProducts(kupac, 3)).Last();
        (await vendor.Send(HttpMethod.Patch, $"/api/products/{product.Id}/quick", new { stockQuantity = 3 })).EnsureSuccessStatusCode();

        var pickup = (decimal qty) => new { items = new[] { new { productId = product.Id, quantity = qty } }, fulfillment = "Pickup", contactPhone = "0641234567" };
        var results = await Task.WhenAll(
            kupac.Send(HttpMethod.Post, "/api/orders", pickup(2)),
            kupac.Send(HttpMethod.Post, "/api/orders", pickup(2)));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(1, (await kupac.Get<Product>($"/api/products/{product.Id}")).StockQuantity);

        // Poslednji kilogram: stanje 0 i proizvod izlazi iz ponude; odbijanje vraća oba
        var last = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", pickup(1))))[0];
        var empty = await kupac.Get<Product>($"/api/products/{product.Id}");
        Assert.Equal(0, empty.StockQuantity);
        Assert.False(empty.IsAvailable);

        (await vendor.Send(HttpMethod.Put, $"/api/orders/{last.Id}/status", new { status = "Rejected", message = "Test" })).EnsureSuccessStatusCode();
        var restored = await kupac.Get<Product>($"/api/products/{product.Id}");
        Assert.Equal(1, restored.StockQuantity);
        Assert.True(restored.IsAvailable);
    }

    [Fact]
    public async Task Paused_vendor_does_not_take_orders()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("milan", "vendor1234");
        var product = (await VendorProducts(kupac, 4))[0];
        (await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability",
            new { ordersPaused = true, openingHours = Array.Empty<object>(), closedDates = Array.Empty<string>() })).EnsureSuccessStatusCode();
        try
        {
            var res = await kupac.Send(HttpMethod.Post, "/api/orders",
                new { items = new[] { new { productId = product.Id, quantity = 1 } }, fulfillment = "Pickup", contactPhone = "0641234567" });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("ne prima porudžbine", await Api.Message(res));
            Assert.False((await kupac.Get<JsonElement>($"/api/products/{product.Id}")).GetProperty("vendorOpen").GetBoolean());
        }
        finally
        {
            await vendor.Send(HttpMethod.Put, "/api/vendors/my-vendor/availability",
                new { ordersPaused = false, openingHours = Array.Empty<object>(), closedDates = Array.Empty<string>() });
        }
    }

    [Fact]
    public async Task Rejecting_an_order_requires_a_reason()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("jelena", "vendor1234");
        var product = (await VendorProducts(kupac, 5))[0];
        var order = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders",
            new { items = new[] { new { productId = product.Id, quantity = 1 } }, fulfillment = "Pickup", contactPhone = "0641234567" })))[0];

        var noReason = await vendor.Send(HttpMethod.Put, $"/api/orders/{order.Id}/status", new { status = "Rejected" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var withReason = await vendor.Send(HttpMethod.Put, $"/api/orders/{order.Id}/status", new { status = "Rejected", message = "Nestalo robe" });
        Assert.Equal(HttpStatusCode.OK, withReason.StatusCode);
        Assert.Equal("Rejected", (await Api.Read<Order>(withReason)).Status);
    }

    // Tok: kupac poruči → prodavac potvrdi → tek tada kuriri vide porudžbinu → kurir prihvati (njegova je)
    // → "Preuzeo sam robu" (kupac vidi U dostavi) → "Isporučeno" (završena)
    [Fact]
    public async Task Courier_flow_follows_vendor_confirmation_pickup_and_delivery()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("bojan", "vendor1234");
        var kurir = await _factory.As("kurir1", "kurir1234");
        (await kurir.Send(HttpMethod.Put, "/api/couriers/me/shift", new { isOnShift = true })).EnsureSuccessStatusCode();
        var product = (await VendorProducts(kupac, 8))[0];
        async Task<int> ActiveCount() => (await kupac.Get<JsonElement>("/api/orders/my-active-count")).GetProperty("count").GetInt32();
        var before = await ActiveCount();
        var order = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];
        // Crveni broj na ikonici "Moje porudžbine" raste odmah posle porudžbine
        Assert.Equal(before + 1, await ActiveCount());

        // Dok prodavac ne potvrdi, kurir je ne vidi i ne može da je prihvati
        Assert.DoesNotContain(await kurir.Get<List<Order>>("/api/couriers/available-orders"), o => o.Id == order.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/accept")).StatusCode);

        (await vendor.Send(HttpMethod.Put, $"/api/orders/{order.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        Assert.Contains(await kurir.Get<List<Order>>("/api/couriers/available-orders"), o => o.Id == order.Id);

        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/accept")).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await kurir.Get<List<Order>>("/api/couriers/available-orders"), o => o.Id == order.Id);

        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/picked-up")).EnsureSuccessStatusCode();
        var forCustomer = await kupac.Get<JsonElement>($"/api/orders/{order.Id}");
        Assert.Equal("InDelivery", forCustomer.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, forCustomer.GetProperty("respondedAt").ValueKind);
        Assert.Equal(JsonValueKind.String, forCustomer.GetProperty("courierAssignedAt").ValueKind);
        Assert.Equal(JsonValueKind.String, forCustomer.GetProperty("pickedUpAt").ValueKind);

        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/delivered")).EnsureSuccessStatusCode();
        forCustomer = await kupac.Get<JsonElement>($"/api/orders/{order.Id}");
        Assert.Equal("Completed", forCustomer.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, forCustomer.GetProperty("deliveredAt").ValueKind);
        Assert.Equal(before, await ActiveCount());
    }

    // Jedna porudžbina = jedna pijaca: korpa sa robom sa dve pijace se odbija i ništa se ne upisuje
    [Fact]
    public async Task Cart_from_two_markets_is_refused()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var futoska = (await VendorProducts(kupac, 1))[0];
        var other = (await VendorProducts(kupac, 4))[0];
        async Task<int> ActiveCount() => (await kupac.Get<JsonElement>("/api/orders/my-active-count")).GetProperty("count").GetInt32();
        var before = await ActiveCount();

        var res = await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((futoska.Id, 1), (other.Id, 1)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("jedne pijace", await Api.Message(res));
        Assert.Equal(before, await ActiveCount());
    }

    // Korpa od dve tezge: kurir ne dobija potvrđenu porudžbinu dok druga tezga ne odgovori; posle odbijanja ostala ide sama
    [Fact]
    public async Task Couriers_get_a_checkout_only_after_every_vendor_answered()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var kurir = await _factory.As("kurir2", "kurir1234");
        (await kurir.Send(HttpMethod.Put, "/api/couriers/me/shift", new { isOnShift = true })).EnsureSuccessStatusCode();
        var dragan = await _factory.As("dragan", "vendor1234");
        var ana = await _factory.As("ana", "vendor1234");
        var orders = await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders",
            CourierOrder(((await VendorProducts(kupac, 6))[0].Id, 1), ((await VendorProducts(kupac, 7))[0].Id, 1))));
        var forDragan = orders.Single(o => o.VendorId == 6);
        var forAna = orders.Single(o => o.VendorId == 7);

        (await dragan.Send(HttpMethod.Put, $"/api/orders/{forDragan.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await kurir.Get<List<Order>>("/api/couriers/available-orders"), o => o.Id == forDragan.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{forDragan.Id}/accept")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await kupac.Get<JsonElement>($"/api/orders/{forDragan.Id}")).GetProperty("courierOfferedAt").ValueKind);

        (await ana.Send(HttpMethod.Put, $"/api/orders/{forAna.Id}/status", new { status = "Rejected", message = "Nestalo je robe" })).EnsureSuccessStatusCode();
        var available = await kurir.Get<List<Order>>("/api/couriers/available-orders");
        Assert.Contains(available, o => o.Id == forDragan.Id);
        Assert.DoesNotContain(available, o => o.Id == forAna.Id);
        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{forDragan.Id}/accept")).EnsureSuccessStatusCode();
    }

    // Prodavac koji ne odgovori na vreme: porudžbina se sama odbija sa razlogom, a ostatak korpe ide kuririma
    [Fact]
    public async Task Unanswered_order_is_rejected_automatically_and_releases_the_checkout()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var kurir = await _factory.As("kurir2", "kurir1234");
        var milan = await _factory.As("milan", "vendor1234");
        var orders = await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders",
            CourierOrder(((await VendorProducts(kupac, 4))[0].Id, 1), ((await VendorProducts(kupac, 5))[0].Id, 1))));
        var forMilan = orders.Single(o => o.VendorId == 4);
        var forJelena = orders.Single(o => o.VendorId == 5);
        (await milan.Send(HttpMethod.Put, $"/api/orders/{forMilan.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();

        // Jelena ćuti: porudžbina je "stara" više od roka
        var pending = await kupac.Get<JsonElement>($"/api/orders/{forJelena.Id}");
        Assert.Equal(JsonValueKind.String, pending.GetProperty("respondBy").ValueKind);
        await using (var conn = new Npgsql.NpgsqlConnection(ApiFactory.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                $"UPDATE \"Orders\" SET \"CreatedAt\" = now() - interval '20 minutes' WHERE \"Id\" = {forJelena.Id}", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IOrderService>().RejectUnansweredOrders();

        var rejected = await kupac.Get<JsonElement>($"/api/orders/{forJelena.Id}");
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal(OrderService.UnansweredReason, rejected.GetProperty("vendorMessage").GetString());
        Assert.Contains(await kurir.Get<List<Order>>("/api/couriers/available-orders"), o => o.Id == forMilan.Id);
    }

    // "Nije dostavljeno": samo posle preuzimanja robe i uz obavezan razlog, koji vide kupac i prodavac
    [Fact]
    public async Task Courier_can_report_not_delivered_only_with_a_reason()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("ivana", "vendor1234");
        var kurir = await _factory.As("kurir1", "kurir1234");
        (await kurir.Send(HttpMethod.Put, "/api/couriers/me/shift", new { isOnShift = true })).EnsureSuccessStatusCode();
        var product = (await VendorProducts(kupac, 9))[0];
        var order = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];
        (await vendor.Send(HttpMethod.Put, $"/api/orders/{order.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/accept")).EnsureSuccessStatusCode();

        // Pre preuzimanja robe ne može
        var tooEarly = await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/not-delivered", new { reason = "Kupac se ne javlja" });
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);

        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/picked-up")).EnsureSuccessStatusCode();
        var noReason = await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/not-delivered", new { reason = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("Upišite razlog zašto porudžbina nije dostavljena.", await Api.Message(noReason));

        var res = await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{order.Id}/not-delivered", new { reason = " Kupac se ne javlja na telefon " });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var forCustomer = await kupac.Get<JsonElement>($"/api/orders/{order.Id}");
        Assert.Equal("NotDelivered", forCustomer.GetProperty("status").GetString());
        Assert.Equal("Kupac se ne javlja na telefon", forCustomer.GetProperty("notDeliveredReason").GetString());
        var forVendor = (await vendor.Get<Paged<JsonElement>>("/api/orders/vendor-orders?finished=true&take=100")).Items
            .Single(o => o.GetProperty("id").GetInt32() == order.Id);
        Assert.Equal("Kupac se ne javlja na telefon", forVendor.GetProperty("notDeliveredReason").GetString());
        Assert.Contains((await kurir.Get<Paged<Order>>("/api/couriers/my-orders?finished=true&take=100")).Items, o => o.Id == order.Id);
    }

    // Prodavac i kurir ne mogu da kupuju, ni sa nalogom ni kao gost dok su prijavljeni
    [Fact]
    public async Task Vendors_and_couriers_cannot_order()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var product = (await VendorProducts(kupac, 1))[0];
        var guestBody = new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } },
            fulfillment = "Pickup", guestName = "Test", guestPhone = "0641234567"
        };

        foreach (var (user, pass) in new[] { ("marko", "vendor1234"), ("kurir1", "kurir1234") })
        {
            var api = await _factory.As(user, pass);
            var own = await api.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)));
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
            Assert.Equal("Prodavci i kuriri ne mogu da poručuju robu. Odjavite se ili koristite kupčev nalog.", await Api.Message(own));
            Assert.Equal(HttpStatusCode.Forbidden, (await api.Send(HttpMethod.Post, "/api/orders/guest", guestBody)).StatusCode);
        }

        // Kupac i gost bez naloga i dalje poručuju
        Assert.Equal(HttpStatusCode.OK, (await (await _factory.As()).Send(HttpMethod.Post, "/api/orders/guest", guestBody)).StatusCode);
    }

    // Prodavac i kurir imaju odvojene tabove: aktivne (i "U dostavi") i završene/odbijene
    [Fact]
    public async Task Vendor_and_courier_lists_split_active_and_finished_orders()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var vendor = await _factory.As("dragan", "vendor1234");
        var kurir = await _factory.As("kurir2", "kurir1234");
        (await kurir.Send(HttpMethod.Put, "/api/couriers/me/shift", new { isOnShift = true })).EnsureSuccessStatusCode();
        var product = (await VendorProducts(kupac, 6))[0];

        var rejected = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];
        var delivering = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];
        (await vendor.Send(HttpMethod.Put, $"/api/orders/{rejected.Id}/status", new { status = "Rejected", message = "Nestalo robe" })).EnsureSuccessStatusCode();
        (await vendor.Send(HttpMethod.Put, $"/api/orders/{delivering.Id}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{delivering.Id}/accept")).EnsureSuccessStatusCode();
        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{delivering.Id}/picked-up")).EnsureSuccessStatusCode();

        var vendorActive = (await vendor.Get<Paged<Order>>("/api/orders/vendor-orders?take=100")).Items;
        var vendorFinished = (await vendor.Get<Paged<Order>>("/api/orders/vendor-orders?finished=true&take=100")).Items;
        Assert.Contains(vendorActive, o => o.Id == delivering.Id && o.Status == "InDelivery");
        Assert.DoesNotContain(vendorActive, o => o.Id == rejected.Id);
        Assert.Contains(vendorFinished, o => o.Id == rejected.Id);
        Assert.DoesNotContain(vendorFinished, o => o.Id == delivering.Id);

        // Dostava u toku ostaje kuriru među aktivnima i posle osvežavanja
        var courierActive = (await kurir.Get<Paged<Order>>("/api/couriers/my-orders")).Items;
        var courierFinished = (await kurir.Get<Paged<Order>>("/api/couriers/my-orders?finished=true&take=100")).Items;
        Assert.Contains(courierActive, o => o.Id == delivering.Id);
        Assert.DoesNotContain(courierFinished, o => o.Id == delivering.Id);

        (await kurir.Send(HttpMethod.Post, $"/api/couriers/orders/{delivering.Id}/delivered")).EnsureSuccessStatusCode();
        courierFinished = (await kurir.Get<Paged<Order>>("/api/couriers/my-orders?finished=true&take=100")).Items;
        Assert.Contains(courierFinished, o => o.Id == delivering.Id && o.Status == "Completed");
    }

    // Porudžbina koja je slučajno ostala otvorena: samo SuperAdmin je može završiti
    [Fact]
    public async Task Super_admin_can_complete_an_order_left_open()
    {
        var kupac = await _factory.As("kupac", "kupac1234");
        var admin = await _factory.As("admin", "admin1234");
        var product = (await VendorProducts(kupac, 7))[0];
        var order = (await Api.Read<List<Order>>(await kupac.Send(HttpMethod.Post, "/api/orders", CourierOrder((product.Id, 1)))))[0];

        Assert.Equal(HttpStatusCode.Forbidden, (await kupac.Send(HttpMethod.Post, $"/api/orders/{order.Id}/admin-complete")).StatusCode);

        var open = await admin.Get<Paged<Order>>("/api/orders/open?take=100");
        Assert.Contains(open.Items, o => o.Id == order.Id);

        var res = await admin.Send(HttpMethod.Post, $"/api/orders/{order.Id}/admin-complete");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var closed = await Api.Read<JsonElement>(res);
        Assert.Equal("Completed", closed.GetProperty("status").GetString());
        // Trag: ko je i kada zatvorio porudžbinu vidi i prodavac
        Assert.False(string.IsNullOrEmpty(closed.GetProperty("closedByAdminName").GetString()));
        var vendor = await _factory.As("ana", "vendor1234");
        var forVendor = (await vendor.Get<Paged<JsonElement>>("/api/orders/vendor-orders?finished=true&take=100")).Items
            .Single(o => o.GetProperty("id").GetInt32() == order.Id);
        Assert.Equal(closed.GetProperty("closedByAdminName").GetString(), forVendor.GetProperty("closedByAdminName").GetString());
        Assert.Equal(JsonValueKind.String, forVendor.GetProperty("closedByAdminAt").ValueKind);
        Assert.DoesNotContain((await admin.Get<Paged<Order>>("/api/orders/open?take=100")).Items, o => o.Id == order.Id);

        var again = await admin.Send(HttpMethod.Post, $"/api/orders/{order.Id}/admin-complete");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("Porudžbina je već zatvorena.", await Api.Message(again));
    }

    [Fact]
    public async Task Customer_report_totals_match_orders_report()
    {
        var admin = await _factory.As("admin", "admin1234");
        var orders = await admin.Get<JsonElement>("/api/reports/orders");
        var customers = await admin.Get<List<JsonElement>>("/api/reports/customers");

        Assert.Equal(orders.GetProperty("totalOrders").GetInt32(), customers.Sum(c => c.GetProperty("orders").GetInt32()));
        Assert.Equal(orders.GetProperty("goodsTotal").GetDecimal(), customers.Sum(c => c.GetProperty("goodsTotal").GetDecimal()));

        var kupac = await _factory.As("kupac", "kupac1234");
        Assert.Equal(HttpStatusCode.Forbidden, (await kupac.Send(HttpMethod.Get, "/api/reports/orders")).StatusCode);
    }
}
