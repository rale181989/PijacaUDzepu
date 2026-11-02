using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Svakog minuta traži porudžbine koje predugo čekaju: prodavca (na čekanju duže od Alerts:PendingMinutes,
// podrazumevano 15) ili kurira (dostava bez kurira duže od Alerts:CourierMinutes, podrazumevano 20).
// Admin dobija obaveštenje (SignalR "OrderWaiting" + push), a prodavcu ponovo zazvoni. Za svaku porudžbinu
// samo jednom (Order.WaitAlertSentAt).
public class OrderWatchdog : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderWatchdog> _logger;
    private readonly TimeSpan _pendingLimit;
    private readonly TimeSpan _courierLimit;

    public OrderWatchdog(IServiceScopeFactory scopeFactory, ILogger<OrderWatchdog> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pendingLimit = TimeSpan.FromMinutes(config.GetValue("Alerts:PendingMinutes", 15));
        _courierLimit = TimeSpan.FromMinutes(config.GetValue("Alerts:CourierMinutes", 20));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Check(); }
            catch (Exception ex) { _logger.LogError(ex, "Provera porudžbina koje čekaju nije uspela."); }
        }
    }

    private async Task Check()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();

        var now = DateTime.UtcNow;
        var pendingBefore = now - _pendingLimit;
        var courierBefore = now - _courierLimit;
        var waiting = await context.Orders
            .Where(o => o.WaitAlertSentAt == null && (
                (o.Status == OrderStatus.Pending && o.CreatedAt < pendingBefore) ||
                (o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && o.CreatedAt < courierBefore
                    && (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.ReadyForPickup))))
            .Select(o => new { o.Id, o.VendorId, VendorName = o.Vendor.Name, o.Status, o.CreatedAt })
            .ToListAsync();
        if (waiting.Count == 0) return;

        var adminIds = await context.Users
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "SuperAdmin"))
            .Select(u => u.Id).ToListAsync();

        foreach (var o in waiting)
        {
            var minutes = (int)(now - o.CreatedAt).TotalMinutes;
            var reason = o.Status == OrderStatus.Pending ? "čeka prodavca" : "čeka kurira";
            await hub.Clients.Group(SupportService.AdminsGroup).SendAsync("OrderWaiting",
                new { orderId = o.Id, vendorName = o.VendorName, reason, minutes });
            await push.SendToUsers(adminIds, $"Porudžbina #{o.Id} {reason}", $"{o.VendorName} · {minutes} min", "/admin-reports");

            // Prodavcu ponovo zazvoni (porudžbina na čekanju)
            if (o.Status == OrderStatus.Pending)
            {
                var vendorUsers = await context.VendorUsers.Where(vu => vu.VendorId == o.VendorId).Select(vu => vu.UserId).ToListAsync();
                await push.SendAlarm(vendorUsers, $"order_{o.Id}", $"Porudžbina #{o.Id} čeka odgovor", $"Čeka već {minutes} min", "/vendor-orders");
            }
        }

        var ids = waiting.Select(o => o.Id).ToList();
        await context.Orders.Where(o => ids.Contains(o.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.WaitAlertSentAt, now));
        _logger.LogWarning("Porudžbine koje predugo čekaju: {Ids}", string.Join(", ", ids));
    }
}
