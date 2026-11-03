using System.Net;
using System.Text.Json;

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
