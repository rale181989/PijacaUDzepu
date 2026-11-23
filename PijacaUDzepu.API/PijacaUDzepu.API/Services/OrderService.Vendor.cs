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

// Prodavac: pregled dana, spisak porudžbina i odgovor na porudžbinu (potvrda, spremno, odbijanje).
public partial class OrderService
{
    // Pregled dana za prodavca
    private record PrepareLine(string ProductName, ProductUnit Unit, decimal Quantity);

    // Zbir stavki po proizvodu i jedinici, uz broj porudžbina u kojima se pojavljuje
    private static List<PrepareItemDto> SumItems(IEnumerable<IEnumerable<PrepareLine>> orders) =>
        orders.SelectMany((items, orderIndex) => items.Select(item => (item, orderIndex)))
            .GroupBy(x => (x.item.ProductName, x.item.Unit))
            .Select(g => new PrepareItemDto
            {
                ProductName = g.Key.ProductName,
                Unit = g.Key.Unit,
                Quantity = g.Sum(x => x.item.Quantity),
                Orders = g.Select(x => x.orderIndex).Distinct().Count()
            })
            .OrderBy(x => x.ProductName)
            .ToList();

    public async Task<VendorTodayDto> GetVendorToday(int vendorId)
    {
        var today = LocalTime.Today;
        var fromUtc = LocalTime.ToUtc(today.ToDateTime(TimeOnly.MinValue));
        var toUtc = LocalTime.ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var active = await _context.Orders
            .Where(o => o.VendorId == vendorId && ActiveStatuses.Contains(o.Status))
            .Select(o => new { o.Status, Items = o.Items.Select(i => new PrepareLine(i.ProductName, i.Unit, i.Quantity)) })
            .ToListAsync();
        var todays = await _context.Orders
            .Where(o => o.VendorId == vendorId && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new { o.Status, o.TotalAmount })
            .ToListAsync();

        return new VendorTodayDto
        {
            Pending = active.Count(o => o.Status == OrderStatus.Pending),
            Confirmed = active.Count(o => o.Status == OrderStatus.Confirmed),
            Ready = active.Count(o => o.Status == OrderStatus.ReadyForPickup),
            InDelivery = active.Count(o => o.Status == OrderStatus.InDelivery),
            // Sprema se samo ono što je prodavac prihvatio; spakovano ("Spremno") se više ne broji,
            // a porudžbine na čekanju su posebno (još mogu biti odbijene)
            ToPrepare = SumItems(active.Where(o => o.Status == OrderStatus.Confirmed).Select(o => o.Items)),
            AwaitingAnswer = SumItems(active.Where(o => o.Status == OrderStatus.Pending).Select(o => o.Items)),
            TodayOrders = todays.Count,
            TodayCompleted = todays.Count(o => o.Status == OrderStatus.Completed),
            TodayGoodsTotal = todays.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
            LowStock = await _context.Products
                .Where(p => p.VendorId == vendorId && p.StockQuantity != null && p.StockQuantity <= 3)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new LowStockDto { ProductId = p.Id, Name = p.Name, Unit = p.Unit, StockQuantity = p.StockQuantity!.Value })
                .ToListAsync()
        };
    }

    // Aktivne i završene (završene, odbijene, otkazane) porudžbine prodavca su odvojeni tabovi
    public async Task<PagedResult<OrderDto>> GetVendorOrders(int vendorId, bool finished, int skip = 0, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var query = OrdersWithDetails().Where(o => o.VendorId == vendorId);
        query = finished
            ? query.Where(o => !OpenStatuses.Contains(o.Status))
            : query.Where(o => OpenStatuses.Contains(o.Status));
        query = query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus, string? message = null)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.VendorId == vendorId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        ValidateStatusTransition(order.Status, newStatus);
        var loadedStatus = order.Status;
        var wasPending = order.Status == OrderStatus.Pending;

        // Kupac mora da zna zašto je porudžbina odbijena
        if (newStatus == OrderStatus.Rejected && string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException(L.T("Upišite razlog odbijanja – kupac će ga dobiti."));

        // Kada robu nosi kurir, porudžbinu završava on potvrdom isporuke
        if (newStatus == OrderStatus.Completed && order.Fulfillment == FulfillmentType.Courier)
            throw new InvalidOperationException(L.T("Ovu porudžbinu dostavlja kurir i on je završava kada je isporuči."));

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        if (wasPending) order.RespondedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(message))
        {
            order.VendorMessage = message.Trim();
            order.VendorMessageAt = DateTime.UtcNow;
        }
        await ClaimStatus(order, loadedStatus);
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyVendor: false);
        if (wasPending)
            await StopVendorAlarm(order);
        if (newStatus == OrderStatus.Rejected)
            await RestoreStock(order);
        // Potvrda ili odbijanje može biti poslednji odgovor u korpi: tada kuriri dobijaju potvrđene porudžbine
        if (wasPending)
            await OfferToCouriersWhenReady(order);
        // Kuriri koji još biraju vide da je roba spremna
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue && order.CourierOfferedAt != null && newStatus == OrderStatus.ReadyForPickup)
            await _hubContext.Clients.Group(CouriersGroup).SendAsync("OrderStatusChanged",
                new { orderId = order.Id, status = newStatus.ToString(), vendorName = order.Vendor.Name, message = (string?)null });
        // Porudžbine iz vremena kada su kuriri obaveštavani odmah: odbijena prestaje da im zvoni
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue && newStatus == OrderStatus.Rejected)
            await NotifyCouriersOrderUnavailable(order.Id);

        // Razlog odbijanja stiže i e-mailom, da ga vidi i gost koji nema otvorenu stranicu za praćenje
        var customerEmail = order.GuestEmail ?? order.Customer?.Email;
        if (newStatus == OrderStatus.Rejected && !string.IsNullOrWhiteSpace(customerEmail))
            SendRejectionEmailInBackground(customerEmail.Trim(), order);

        return MapToDto(order);
    }

    public async Task<int> GetVendorPendingCount(int vendorId)
    {
        return await _context.Orders
            .CountAsync(o => o.VendorId == vendorId && o.Status == OrderStatus.Pending);
    }
}
