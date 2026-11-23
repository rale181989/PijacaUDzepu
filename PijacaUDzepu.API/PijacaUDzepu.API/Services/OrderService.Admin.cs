using PijacaUDzepu.API.Helpers;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Automatsko odbijanje porudžbina bez odgovora (OrderWatchdog) i administrator: otvorene porudžbine i zatvaranje.
public partial class OrderService
{
    // OrderWatchdog: porudžbine na koje prodavac nije odgovorio na vreme odbijaju se same, sa razlogom za kupca.
    // Uslovni upis (još na čekanju), pa porudžbina koju je prodavac upravo potvrdio ostaje potvrđena.
    public async Task<int> RejectUnansweredOrders()
    {
        var before = DateTime.UtcNow.AddMinutes(-AutoRejectMinutes);
        // Samo porudžbine iz poslednja 2 sata: posle nadogradnje se ne odbijaju odjednom stare zaboravljene porudžbine
        // (uz talas e-mailova kupcima); njih zatvara administrator u "Otvorene porudžbine"
        var notBefore = DateTime.UtcNow.AddHours(-AutoRejectWindowHours);
        var ids = await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < before && o.CreatedAt > notBefore)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .Take(100)
            .ToListAsync();

        var rejected = 0;
        foreach (var id in ids)
        {
            var now = DateTime.UtcNow;
            var updated = await _context.Orders
                .Where(o => o.Id == id && o.Status == OrderStatus.Pending)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(o => o.Status, OrderStatus.Rejected)
                    .SetProperty(o => o.UpdatedAt, now)
                    .SetProperty(o => o.VendorMessage, UnansweredReason)
                    .SetProperty(o => o.VendorMessageAt, (DateTime?)now));
            if (updated == 0) continue;
            rejected++;

            var order = await OrdersWithDetails().FirstAsync(o => o.Id == id);
            await NotifyStatusChanged(order);
            await StopVendorAlarm(order);
            await RestoreStock(order);
            await _pushService.SendToUsers(await VendorUserIds(order.VendorId),
                $"Porudžbina #{order.Id} je otkazana",
                $"Niste odgovorili za {AutoRejectMinutes} min, pa je porudžbina automatski odbijena.", "/vendor-orders");
            await OfferToCouriersWhenReady(order);

            var customerEmail = order.GuestEmail ?? order.Customer?.Email;
            if (!string.IsNullOrWhiteSpace(customerEmail))
                SendRejectionEmailInBackground(customerEmail.Trim(), order);
        }
        if (rejected > 0)
            _logger.LogWarning("Automatski odbijeno porudžbina bez odgovora prodavca: {Count}", rejected);
        return rejected;
    }

    // SuperAdmin: sve porudžbine koje još nisu zatvorene, najstarije prve (zaboravljene se vide odmah)
    public async Task<PagedResult<OrderDto>> GetOpenOrders(int skip = 0, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var query = OrdersWithDetails().Where(o => OpenStatuses.Contains(o.Status));
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto> { Items = items, TotalCount = totalCount, HasMore = skip + take < totalCount };
    }

    // SuperAdmin zatvara porudžbinu koja je slučajno ostala otvorena (prodavac ili kurir zaboravio da je završi).
    // Kupac ne dobija push "isporučeno" za staru porudžbinu; prestaje zvono prodavcu i kuririma.
    public async Task<OrderDto> AdminCompleteOrder(int orderId, int adminId)
    {
        var order = await OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        if (!OpenStatuses.Contains(order.Status))
            throw new InvalidOperationException(L.T("Porudžbina je već zatvorena."));

        var loadedStatus = order.Status;
        var wasPending = order.Status == OrderStatus.Pending;
        order.Status = OrderStatus.Completed;
        order.UpdatedAt = DateTime.UtcNow;
        if (wasPending) order.RespondedAt = DateTime.UtcNow;
        order.ClosedByAdminId = adminId;
        order.ClosedByAdminAt = DateTime.UtcNow;
        await ClaimStatus(order, loadedStatus);
        await _context.SaveChangesAsync();
        // Ime admina za trag u odgovoru
        await _context.Entry(order).Reference(o => o.ClosedByAdmin).LoadAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        if (wasPending)
            await StopVendorAlarm(order);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);
        if (wasPending)
            await OfferToCouriersWhenReady(order);

        return MapToDto(order);
    }
}
