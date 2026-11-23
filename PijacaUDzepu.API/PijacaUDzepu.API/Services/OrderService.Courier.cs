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

// Kurir: ponuda tek kad odgovore sve tezge iz korpe, prihvatanje (cela korpa jednom kuriru), preuzimanje,
// isporuka, nedostavljanje, obračun i usluga kurira jednom po korpi.
public partial class OrderService
{
    public async Task<List<OrderDto>> GetAvailableCourierOrders(int courierId)
    {
        var orders = await OrdersWithDetails()
            .Where(o => o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && o.CourierOfferedAt != null && CourierOfferStatuses.Contains(o.Status))
            .Where(o => !_context.CourierDeclines.Any(d => d.OrderId == o.Id && d.CourierId == courierId))
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync();

        return orders.Select(MapToCourierPreview).ToList();
    }

    // Aktivne dostave kurir vidi sve (ima ih malo); završene/otkazane idu po stranama
    public async Task<PagedResult<OrderDto>> GetCourierOrders(int courierId, bool finished, int skip = 0, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var query = OrdersWithDetails().Where(o => o.CourierId == courierId);
        query = finished
            ? query.Where(o => !OpenStatuses.Contains(o.Status))
            : query.Where(o => OpenStatuses.Contains(o.Status));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip(finished ? skip : 0)
            .Take(finished ? take : 100)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto> { Items = items, TotalCount = totalCount, HasMore = finished && skip + take < totalCount };
    }

    // Porudžbina iz korpe sa više tezgi se prihvata samo cela (AcceptCourierCheckout): kupac dobija jednog kurira
    // i jednu turu, a ne dva kurira za istu adresu
    public async Task<OrderDto> AcceptCourierOrder(int orderId, int courierId)
    {
        var checkoutId = await _context.Orders.Where(o => o.Id == orderId).Select(o => o.CheckoutId).FirstOrDefaultAsync();
        if (checkoutId != null && await _context.Orders.CountAsync(o => o.CheckoutId == checkoutId
                && o.Fulfillment == FulfillmentType.Courier && OpenStatuses.Contains(o.Status)) > 1)
            return (await AcceptCourierCheckout(checkoutId.Value, courierId)).FirstOrDefault(o => o.Id == orderId)
                ?? throw new InvalidOperationException(L.T("Porudžbinu je već preuzeo drugi kurir ili više nije dostupna."));

        if (!await _context.CourierProfiles.AnyAsync(c => c.UserId == courierId && c.IsOnShift))
            throw new UnauthorizedAccessException(L.T("Uključite smenu da biste prihvatali porudžbine."));

        var now = DateTime.UtcNow;

        // Jedan uslovni UPDATE: od više kurira koji istovremeno prihvate, upis uspeva samo prvom
        var updated = await _context.Orders
            .Where(o => o.Id == orderId && o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && o.CourierOfferedAt != null && CourierOfferStatuses.Contains(o.Status))
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.CourierId, (int?)courierId)
                .SetProperty(o => o.CourierAssignedAt, (DateTime?)now)
                .SetProperty(o => o.UpdatedAt, now));

        if (updated == 0)
        {
            if (!await _context.Orders.AnyAsync(o => o.Id == orderId))
                throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));
            var state = await _context.Orders.Where(o => o.Id == orderId)
                .Select(o => new { o.Fulfillment, o.CourierId, o.CourierOfferedAt, o.Status }).FirstAsync();
            if (state.Fulfillment == FulfillmentType.Pickup)
                throw new InvalidOperationException(L.T("Ovu porudžbinu kupac preuzima sam na tezgi."));
            if (state.CourierId == null && state.CourierOfferedAt == null && OpenStatuses.Contains(state.Status))
                throw new InvalidOperationException(L.T("Porudžbina još nije spremna za kurira: čeka se odgovor prodavaca."));
            throw new InvalidOperationException(L.T("Porudžbinu je već preuzeo drugi kurir ili više nije dostupna."));
        }

        return await NotifyCourierAccepted(orderId);
    }

    // Posle prihvatanja: nestaje kod ostalih kurira, prodavac i kupac saznaju ko dolazi
    private async Task<OrderDto> NotifyCourierAccepted(int orderId)
    {
        var order = await OrdersWithDetails().FirstAsync(o => o.Id == orderId);
        var courierName = $"{order.Courier!.FirstName} {order.Courier.LastName}".Trim();

        await NotifyCouriersOrderUnavailable(order.Id);

        var assignedPayload = new { orderId = order.Id, courierName, courierPhone = order.Courier.PhoneNumber };
        await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("CourierAssigned", assignedPayload);
        if (order.CustomerId.HasValue)
            await _hubContext.Clients.Group($"user_{order.CustomerId.Value}").SendAsync("CourierAssigned", assignedPayload);
        if (order.TrackingCode != null)
            await _trackingHub.Clients.Group(TrackingHub.GroupName(order.TrackingCode)).SendAsync("CourierAssigned", assignedPayload);

        await _pushService.SendToUsers(await VendorUserIds(order.VendorId),
            $"Kurir za porudžbinu #{order.Id}", $"{courierName} dolazi po robu.", "/vendor-orders");
        if (order.CustomerId.HasValue)
            await _pushService.SendToUsers(new[] { order.CustomerId.Value },
                "Dodeljen je kurir", $"{courierName} preuzima robu kod {order.Vendor.Name} i donosi vam je.", $"/order-detail/{order.Id}");

        return MapToDto(order);
    }

    public async Task DeclineCourierOrder(int orderId, int courierId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.Fulfillment == FulfillmentType.Courier)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));
        // Porudžbina koja još nije ponuđena kuririma ne može da se odbije unapred (posle je kurir ne bi ni video)
        if (order.CourierOfferedAt == null)
            throw new InvalidOperationException(L.T("Porudžbina još nije spremna za kurira: čeka se odgovor prodavaca."));

        if (!await _context.CourierDeclines.AnyAsync(d => d.OrderId == orderId && d.CourierId == courierId))
        {
            _context.CourierDeclines.Add(new CourierDecline { OrderId = orderId, CourierId = courierId });
            await _context.SaveChangesAsync();
        }

        // Nestaje sa spiska i prestaje da zvoni samo ovom kuriru (i na njegovim drugim uređajima)
        await _hubContext.Clients.Group($"user_{courierId}").SendAsync("CourierOrderUnavailable", new { orderId = order.Id });
        await _pushService.CancelAlarm(new[] { courierId }, CourierAlarmTag(order.Id));
    }

    // "Prihvati sve": cela korpa (više tezgi, ista adresa) ide jednom kuriru, jednim uslovnim upisom. Ako je bilo
    // koju porudžbinu iz korpe već uzeo drugi kurir, ne prihvata se ništa; kada dva kurira prihvate istovremeno, baza
    // pušta samo prvog (drugi upis ne nađe slobodne redove ili dobije deadlock), pa se korpa nikad ne deli.
    public async Task<List<OrderDto>> AcceptCourierCheckout(Guid checkoutId, int courierId)
    {
        if (!await _context.CourierProfiles.AnyAsync(c => c.UserId == courierId && c.IsOnShift))
            throw new UnauthorizedAccessException(L.T("Uključite smenu da biste prihvatali porudžbine."));

        var now = DateTime.UtcNow;
        int updated;
        try
        {
            updated = await _context.Orders
                .Where(o => o.CheckoutId == checkoutId && o.Fulfillment == FulfillmentType.Courier && o.CourierId == null
                    && o.CourierOfferedAt != null && CourierOfferStatuses.Contains(o.Status))
                .Where(o => !_context.Orders.Any(s => s.CheckoutId == checkoutId && s.CourierId != null && s.CourierId != courierId
                    && OpenStatuses.Contains(s.Status)))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(o => o.CourierId, (int?)courierId)
                    .SetProperty(o => o.CourierAssignedAt, (DateTime?)now)
                    .SetProperty(o => o.UpdatedAt, now));
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40P01")
        {
            updated = 0;
        }
        if (updated == 0)
        {
            var ready = await _context.Orders.AnyAsync(o => o.CheckoutId == checkoutId && o.CourierId == null
                && o.CourierOfferedAt == null && OpenStatuses.Contains(o.Status));
            throw new InvalidOperationException(ready
                ? L.T("Porudžbina još nije spremna za kurira: čeka se odgovor prodavaca.")
                : L.T("Ove porudžbine su već preuzete ili više nisu dostupne."));
        }

        var ids = await _context.Orders
            .Where(o => o.CheckoutId == checkoutId && o.CourierId == courierId && o.CourierAssignedAt == now)
            .Select(o => o.Id)
            .ToListAsync();
        var accepted = new List<OrderDto>();
        foreach (var id in ids)
            accepted.Add(await NotifyCourierAccepted(id));
        return accepted;
    }

    // Kurir vraća porudžbinu koju je odbio (dodir "Vrati" odmah posle "Odbij")
    public async Task UndoDeclineCourierOrder(int orderId, int courierId)
    {
        await _context.CourierDeclines.Where(d => d.OrderId == orderId && d.CourierId == courierId).ExecuteDeleteAsync();
    }

    // Dnevni obračun kurira za lokalni datum: porudžbine isporučene tog dana
    public async Task<CourierSettlementDto> GetCourierSettlement(int courierId, DateOnly date)
    {
        var fromUtc = LocalTime.ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var toUtc = LocalTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var delivered = await _context.Orders
            .Where(o => o.CourierId == courierId && o.Status == OrderStatus.Completed && o.DeliveredAt >= fromUtc && o.DeliveredAt < toUtc)
            .OrderBy(o => o.DeliveredAt)
            .Select(o => new
            {
                o.Id, o.VendorId, VendorName = o.Vendor.Name, MarketName = o.Vendor.Market.Name,
                StallLabel = o.Vendor.Stall != null ? o.Vendor.Stall.Label : null, o.Vendor.Phones,
                o.DeliveryAddress, o.TotalAmount, o.DeliveryFee, DeliveredAt = o.DeliveredAt!.Value
            })
            .ToListAsync();

        // Nije dostavljeno tog dana: roba je kod kurira i vraća se na tezgu
        var returns = await _context.Orders
            .Where(o => o.CourierId == courierId && o.Status == OrderStatus.NotDelivered && o.NotDeliveredAt >= fromUtc && o.NotDeliveredAt < toUtc)
            .OrderBy(o => o.NotDeliveredAt)
            .Select(o => new SettlementReturnDto
            {
                OrderId = o.Id, VendorName = o.Vendor.Name, MarketName = o.Vendor.Market.Name,
                StallLabel = o.Vendor.Stall != null ? o.Vendor.Stall.Label : null,
                VendorPhone = o.Vendor.Phones.FirstOrDefault(),
                Reason = o.NotDeliveredReason,
                Items = string.Join(", ", o.Items.Select(i => i.ProductName))
            })
            .ToListAsync();

        return new CourierSettlementDto
        {
            Date = date,
            Delivered = delivered.Count,
            InProgress = await _context.Orders.CountAsync(o => o.CourierId == courierId && OpenStatuses.Contains(o.Status)),
            Collected = delivered.Sum(d => d.TotalAmount + d.DeliveryFee),
            OwedToVendors = delivered.Sum(d => d.TotalAmount),
            Earned = delivered.Sum(d => d.DeliveryFee),
            Vendors = delivered
                .GroupBy(d => new { d.VendorId, d.VendorName, d.MarketName, d.StallLabel })
                .Select(g => new VendorSettlementDto
                {
                    VendorId = g.Key.VendorId, VendorName = g.Key.VendorName, MarketName = g.Key.MarketName,
                    StallLabel = g.Key.StallLabel, VendorPhone = g.First().Phones.FirstOrDefault(),
                    Orders = g.Count(), Amount = g.Sum(d => d.TotalAmount)
                })
                .OrderByDescending(v => v.Amount)
                .ToList(),
            Deliveries = delivered.Select(d => new SettlementDeliveryDto
            {
                OrderId = d.Id, DeliveredAt = d.DeliveredAt, VendorName = d.VendorName,
                DeliveryAddress = d.DeliveryAddress, Goods = d.TotalAmount, Fee = d.DeliveryFee
            }).ToList(),
            Returns = returns
        };
    }

    public async Task<OrderDto> MarkPickedUp(int orderId, int courierId)
    {
        var order = await LoadCourierOrder(orderId, courierId);
        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.ReadyForPickup))
            throw new InvalidOperationException(L.T("Robu možete preuzeti tek kada prodavac potvrdi porudžbinu."));

        order.Status = OrderStatus.InDelivery;
        order.PickedUpAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCourier: false);
        return MapToDto(order);
    }

    public async Task<OrderDto> MarkDelivered(int orderId, int courierId)
    {
        var order = await LoadCourierOrder(orderId, courierId);
        if (order.Status != OrderStatus.InDelivery)
            throw new InvalidOperationException(L.T("Isporuku možete potvrditi tek nakon što preuzmete robu."));

        order.Status = OrderStatus.Completed;
        order.DeliveredAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCourier: false);
        return MapToDto(order);
    }

    // Kurir nije uspeo da preda robu (kupac nije tu, pogrešna adresa...). Razlog je obavezan i vide ga
    // kupac, prodavac i administrator; roba se vraća prodavcu, pa se vraća i stanje.
    public async Task<OrderDto> MarkNotDelivered(int orderId, int courierId, string reason)
    {
        var order = await LoadCourierOrder(orderId, courierId);
        if (order.Status != OrderStatus.InDelivery)
            throw new InvalidOperationException(L.T("Nedostavljanje možete prijaviti tek nakon što preuzmete robu."));
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(L.T("Upišite razlog zašto porudžbina nije dostavljena."));

        order.Status = OrderStatus.NotDelivered;
        order.NotDeliveredReason = reason.Trim();
        order.NotDeliveredAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await MoveDeliveryFee(order);
        await NotifyStatusChanged(order, notifyCourier: false);
        await RestoreStock(order);

        var courierName = $"{order.Courier!.FirstName} {order.Courier.LastName}".Trim();
        await _pushService.SendToUsers(await VendorUserIds(order.VendorId),
            $"Porudžbina #{order.Id} nije dostavljena", $"{courierName}: {order.NotDeliveredReason}. Roba se vraća na tezgu.", "/vendor-orders");
        var adminIds = await _context.Users
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "SuperAdmin"))
            .Select(u => u.Id).ToListAsync();
        await _pushService.SendToUsers(adminIds,
            $"Porudžbina #{order.Id} nije dostavljena", $"{order.Vendor.Name} · {courierName}: {order.NotDeliveredReason}", "/admin-reports");

        return MapToDto(order);
    }

    private async Task<Order> LoadCourierOrder(int orderId, int courierId) =>
        await OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == orderId && o.CourierId == courierId)
        ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

    // Kuriri dobijaju porudžbine jedne korpe tek kada se izjasne svi prodavci iz nje (nijedna nije na čekanju),
    // da kurir ne krene na pola rute. Odbijene ne idu, ostale idu automatski. Uslovni upis (CourierOfferedAt == null),
    // pa kada dva prodavca odgovore istovremeno, kuriri ipak dobiju porudžbinu samo jednom.
    private async Task OfferToCouriersWhenReady(Order order)
    {
        if (order.Fulfillment != FulfillmentType.Courier) return;
        await MoveDeliveryFee(order);
        var checkout = order.CheckoutId != null
            ? _context.Orders.Where(o => o.CheckoutId == order.CheckoutId)
            : _context.Orders.Where(o => o.Id == order.Id);
        if (await checkout.AnyAsync(o => o.Status == OrderStatus.Pending)) return;

        var ids = await checkout
            .Where(o => o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && o.CourierOfferedAt == null && CourierOfferStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToListAsync();
        foreach (var id in ids)
        {
            var now = DateTime.UtcNow;
            var offered = await _context.Orders
                .Where(o => o.Id == id && o.CourierOfferedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.CourierOfferedAt, (DateTime?)now));
            if (offered == 0) continue;
            // Prodavac koji je poslednji odgovorio odmah vidi "Čeka se kurir" (odgovor nosi vreme ponude)
            if (id == order.Id) order.CourierOfferedAt = now;

            var offer = await OrdersWithDetails().FirstAsync(o => o.Id == id);
            offer.CourierOfferedAt = now;
            await NotifyCouriersNewOrder(offer);
        }
    }

    // Usluga kurira se plaća jednom po korpi i stoji na jednoj njenoj porudžbini. Kada ta porudžbina propadne
    // (odbijena, otkazana, nije dostavljena), usluga prelazi na najstariju porudžbinu iste korpe koja nije propala,
    // da je kupac ipak plati za ostale tezge, a kurir je vidi u obračunu. Ako je propala cela korpa, ostaje gde je.
    private static readonly OrderStatus[] FailedStatuses =
        { OrderStatus.Rejected, OrderStatus.Cancelled, OrderStatus.NotDelivered };

    private async Task MoveDeliveryFee(Order order)
    {
        if (order.CheckoutId == null || order.Fulfillment != FulfillmentType.Courier) return;
        var orders = await _context.Orders
            .Where(o => o.CheckoutId == order.CheckoutId && o.Fulfillment == FulfillmentType.Courier)
            .OrderBy(o => o.Id)
            .ToListAsync();
        var alive = orders.Where(o => !FailedStatuses.Contains(o.Status)).ToList();
        // Nema žive porudžbine ili neka već nosi uslugu (i stare korpe, gde je usluga bila na svakoj tezgi)
        if (alive.Count == 0 || alive.Any(o => o.DeliveryFee > 0)) return;
        var holder = orders.FirstOrDefault(o => FailedStatuses.Contains(o.Status) && o.DeliveryFee > 0);
        if (holder == null) return;

        alive[0].DeliveryFee = holder.DeliveryFee;
        holder.DeliveryFee = 0;
        await _context.SaveChangesAsync();
    }

    // Prodavac je potvrdio dostavu: kuriri na smeni je dobijaju i zvoni im dok je neko ne prihvati
    private async Task NotifyCouriersNewOrder(Order order)
    {
        var onShift = await _context.CourierProfiles
            .Where(c => c.IsOnShift && c.User.IsActive)
            .Select(c => c.UserId)
            .ToListAsync();
        if (onShift.Count == 0) return;

        await _hubContext.Clients.Groups(onShift.Select(id => $"user_{id}").ToList())
            .SendAsync("CourierNewOrder", MapToCourierPreview(order));
        // Zvoni kuriru dok porudžbinu ne prihvati ili odbije (ili dok je ne preuzme drugi kurir)
        await _pushService.SendAlarm(onShift, CourierAlarmTag(order.Id),
            "Nova porudžbina za dostavu",
            $"{order.Vendor.Name} ({order.Vendor.Market?.Name}) → {order.DeliveryAddress}",
            "/courier-orders");
    }

    // Porudžbina više nije slobodna (prihvaćena, odbijena ili otkazana): nestaje kod svih kurira i prestaje da im zvoni
    private async Task NotifyCouriersOrderUnavailable(int orderId)
    {
        await _hubContext.Clients.Group(CouriersGroup).SendAsync("CourierOrderUnavailable", new { orderId });
        var courierIds = await _context.CourierProfiles.Select(c => c.UserId).ToListAsync();
        await _pushService.CancelAlarm(courierIds, CourierAlarmTag(orderId));
    }
}
