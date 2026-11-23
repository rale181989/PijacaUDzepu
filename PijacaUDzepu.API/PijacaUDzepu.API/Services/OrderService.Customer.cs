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

// Kupac i gost: moje porudžbine, otkazivanje, ocena, praćenje po kodu i prelazak gostujućih porudžbina na nalog.
public partial class OrderService
{
    public async Task<PagedResult<OrderDto>> GetCustomerOrders(int customerId, int skip = 0, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);
        var query = OrdersWithDetails()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = (await query
            .Skip(skip)
            .Take(take)
            .Select(o => MapToDto(o))
            .ToListAsync()).Select(ForCustomer).ToList();

        return new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<OrderDto> CancelOrder(int orderId, int customerId)
    {
        if (!CustomerCancelEnabled)
            throw new InvalidOperationException(L.T(CancelDisabledMessage));

        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException(L.T("Samo porudžbine na čekanju mogu biti otkazane."));

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        await StopVendorAlarm(order);
        await RestoreStock(order);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);
        await OfferToCouriersWhenReady(order);

        return MapToDto(order);
    }

    // Ocena posle preuzimanja: prijavljeni kupac (customerId) ili gost preko koda za praćenje
    public async Task<OrderDto> RateOrder(int orderId, int? customerId, string? trackingCode, OrderRatingInputDto dto)
    {
        var order = await OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == orderId
                && (customerId != null ? o.CustomerId == customerId : o.TrackingCode != null && o.TrackingCode == trackingCode))
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        if (order.Status != OrderStatus.Completed)
            throw new InvalidOperationException(L.T("Porudžbinu možete oceniti kada je preuzmete."));
        if (order.Rating != null)
            throw new InvalidOperationException(L.T("Porudžbina je već ocenjena."));

        order.Rating = new OrderRating
        {
            OrderId = order.Id,
            VendorScore = dto.VendorScore,
            CourierScore = order.CourierId != null ? dto.CourierScore : null,
            Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim()
        };
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("OrderRated", new { orderId = order.Id, vendorScore = dto.VendorScore });
        return customerId != null ? ForCustomer(MapToDto(order)) : MapToTrackingDto(order);
    }

    public async Task<List<OrderDto>> GetTrackedOrders(string code)
    {
        var orders = await LoadTrackedOrders(code);
        return orders.Select(MapToTrackingDto).ToList();
    }

    public async Task<OrderDto> CancelTrackedOrder(string code, int orderId)
    {
        if (!CustomerCancelEnabled)
            throw new InvalidOperationException(L.T(CancelDisabledMessage));

        var order = (await LoadTrackedOrders(code)).FirstOrDefault(o => o.Id == orderId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException(L.T("Samo porudžbine na čekanju mogu biti otkazane."));

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        await StopVendorAlarm(order);
        await RestoreStock(order);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);
        await OfferToCouriersWhenReady(order);

        return MapToTrackingDto(order);
    }

    private async Task<List<Order>> LoadTrackedOrders(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
            throw new KeyNotFoundException(L.T("Link za praćenje nije ispravan."));

        var orders = await OrdersWithDetails()
            .Where(o => o.TrackingCode == code)
            .OrderBy(o => o.Id)
            .ToListAsync();

        var stillActive = orders.Any(o => ActiveStatuses.Contains(o.Status) || o.Status == OrderStatus.InDelivery);
        if (orders.Count == 0 || (!stillActive && orders.Max(o => o.UpdatedAt) < DateTime.UtcNow - TrackingValidity))
            throw new KeyNotFoundException(L.T("Link za praćenje nije ispravan ili je istekao."));

        return orders;
    }

    private static string NewTrackingCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    private static string? MaskAddress(string? address) =>
        address == null ? null : Regex.Replace(address, @"\S*\d\S*", "•••");

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return phone;
        var digits = phone.Count(char.IsDigit);
        var seen = 0;
        return new string(phone.Select(c => char.IsDigit(c) && ++seen <= digits - 2 ? '•' : c).ToArray());
    }

    // Kupac se registrovao ili prijavio na uređaju sa kog je ranije poručivao kao gost: te porudžbine
    // (po kodovima za praćenje koje taj uređaj čuva) prelaze na njegov nalog i vide se u "Moje porudžbine".
    public async Task<int> ClaimGuestOrders(int customerId, List<string> trackingCodes)
    {
        var codes = trackingCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().Take(20).ToList();
        if (codes.Count == 0) return 0;
        return await _context.Orders
            .Where(o => o.CustomerId == null && o.TrackingCode != null && codes.Contains(o.TrackingCode))
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.CustomerId, (int?)customerId));
    }

    public Task<int> GetCustomerActiveCount(int customerId) =>
        _context.Orders.CountAsync(o => o.CustomerId == customerId && OpenStatuses.Contains(o.Status));
}
