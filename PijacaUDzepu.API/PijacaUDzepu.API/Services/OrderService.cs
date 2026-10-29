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

public class OrderService : IOrderService
{
    private readonly DataContext _context;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly IHubContext<TrackingHub> _trackingHub;
    private readonly IPushService _pushService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderService> _logger;
    private readonly IConfiguration _config;

    public OrderService(DataContext context, IHubContext<OrderHub> hubContext, IHubContext<TrackingHub> trackingHub,
        IPushService pushService, IEmailService emailService, ILogger<OrderService> logger, IConfiguration config)
    {
        _emailService = emailService;
        _logger = logger;
        _context = context;
        _hubContext = hubContext;
        _trackingHub = trackingHub;
        _pushService = pushService;
        _config = config;
    }

    // Link za praćenje važi dok je porudžbina u toku i još 30 dana posle poslednje promene
    private static readonly TimeSpan TrackingValidity = TimeSpan.FromDays(30);

    // Cena Eko kurira po porudžbini (po prodavcu); podešava se sa Delivery__CourierFee
    public decimal CourierFee => _config.GetValue<decimal?>("Delivery:CourierFee") ?? 300m;

    // Kuriri vide samo porudžbine koje su još u toku
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup };

    private const string CouriersGroup = "couriers";

    public async Task<List<OrderDto>> CreateOrders(int customerId, OrderInputDto dto)
    {
        var address = ValidateAddress(dto.Fulfillment, dto.DeliveryAddress);
        var orders = await BuildOrders(dto.Items, dto.Note, dto.Fulfillment);
        foreach (var order in orders)
        {
            order.CustomerId = customerId;
            order.ContactPhone = dto.ContactPhone.Trim();
            order.DeliveryAddress = address;
        }

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        await NotifyNewOrders(orders);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderDto>> CreateGuestOrders(GuestOrderInputDto dto)
    {
        var address = ValidateAddress(dto.Fulfillment, dto.GuestAddress);
        var orders = await BuildOrders(dto.Items, dto.Note, dto.Fulfillment);
        var trackingCode = NewTrackingCode();
        foreach (var order in orders)
        {
            order.TrackingCode = trackingCode;
            order.GuestName = dto.GuestName.Trim();
            order.GuestEmail = string.IsNullOrWhiteSpace(dto.GuestEmail) ? null : dto.GuestEmail.Trim();
            order.ContactPhone = dto.GuestPhone.Trim();
            order.DeliveryAddress = address;
        }

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        await NotifyNewOrders(orders);

        if (!string.IsNullOrWhiteSpace(dto.GuestEmail))
            SendTrackingEmailInBackground(dto.GuestEmail.Trim(), dto.GuestName.Trim(), trackingCode, orders);

        // Kod za praćenje dobija samo gost koji je poručio; prodavcu i kuririma se ne prikazuje
        return orders.Select(o =>
        {
            var dto = MapToDto(o);
            dto.TrackingCode = o.TrackingCode;
            return dto;
        }).ToList();
    }

    // E-mail se šalje u pozadini: kupac ne čeka SMTP, a greška pri slanju ne obara porudžbinu.
    // EmailService ne koristi DbContext, pa sme da radi posle završetka zahteva.
    private void SendTrackingEmailInBackground(string email, string guestName, string trackingCode, List<Order> orders)
    {
        var emailService = _emailService;
        var logger = _logger;
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendTrackingLink(email, guestName, trackingCode, orders);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Slanje linka za praćenje na {Email} nije uspelo.", email);
            }
        });
    }

    private static string? ValidateAddress(FulfillmentType fulfillment, string? address)
    {
        var trimmed = address?.Trim();
        // Oblik adrese se ne proverava (postoje i kratki nazivi ulica, brojevi sa slovom, "bb"...)
        if (fulfillment == FulfillmentType.Courier && string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException("Za dostavu kurirom unesite adresu.");
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task<List<Order>> BuildOrders(List<OrderItemInputDto> items, string? note, FulfillmentType fulfillment)
    {
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var unavailable = products.Where(p => !p.IsAvailable || !p.Vendor.IsActive).ToList();
        if (unavailable.Any())
            throw new InvalidOperationException(
                $"Sledeći proizvodi nisu dostupni: {string.Join(", ", unavailable.Select(p => p.Name))}");

        var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missingIds.Any())
            throw new KeyNotFoundException($"Proizvodi sa ID {string.Join(", ", missingIds)} nisu pronađeni.");

        var groupedByVendor = items
            .GroupBy(item => products.First(p => p.Id == item.ProductId).VendorId)
            .ToList();

        var orders = new List<Order>();

        foreach (var group in groupedByVendor)
        {
            var order = new Order
            {
                VendorId = group.Key,
                Vendor = products.First(p => p.VendorId == group.Key).Vendor,
                Note = note,
                Status = OrderStatus.Pending,
                Fulfillment = fulfillment,
                DeliveryFee = fulfillment == FulfillmentType.Courier ? CourierFee : 0,
                Items = new List<OrderItem>()
            };

            foreach (var item in group)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var totalPrice = product.Price * item.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    Unit = product.Unit,
                    UnitPrice = product.Price,
                    TotalPrice = totalPrice
                });
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
            orders.Add(order);
        }

        return orders;
    }

    public async Task<PagedResult<OrderDto>> GetCustomerOrders(int customerId, int skip = 0, int take = 20)
    {
        var query = OrdersWithDetails()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt);

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

    public async Task<PagedResult<OrderDto>> GetVendorOrders(int vendorId, int skip = 0, int take = 20)
    {
        var query = OrdersWithDetails()
            .Where(o => o.VendorId == vendorId)
            .OrderByDescending(o => o.CreatedAt);

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

    public async Task<OrderDto> GetOrderById(int orderId)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        return MapToDto(order);
    }

    public async Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus, string? message = null)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        ValidateStatusTransition(order.Status, newStatus);

        // Kada robu nosi kurir, porudžbinu završava on potvrdom isporuke
        if (newStatus == OrderStatus.Completed && order.Fulfillment == FulfillmentType.Courier)
            throw new InvalidOperationException("Ovu porudžbinu dostavlja kurir i on je završava kada je isporuči.");

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(message))
        {
            order.VendorMessage = message.Trim();
            order.VendorMessageAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyVendor: false);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue && newStatus == OrderStatus.Rejected)
            await NotifyCouriersOrderUnavailable(order.Id);

        return MapToDto(order);
    }

    public async Task<OrderDto> CancelOrder(int orderId, int customerId)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Samo porudžbine na čekanju mogu biti otkazane.");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);

        return MapToDto(order);
    }

    public async Task<List<OrderDto>> GetTrackedOrders(string code)
    {
        var orders = await LoadTrackedOrders(code);
        return orders.Select(MapToTrackingDto).ToList();
    }

    public async Task<OrderDto> CancelTrackedOrder(string code, int orderId)
    {
        var order = (await LoadTrackedOrders(code)).FirstOrDefault(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Samo porudžbine na čekanju mogu biti otkazane.");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);

        return MapToTrackingDto(order);
    }

    private async Task<List<Order>> LoadTrackedOrders(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
            throw new KeyNotFoundException("Link za praćenje nije ispravan.");

        var orders = await OrdersWithDetails()
            .Where(o => o.TrackingCode == code)
            .OrderBy(o => o.Id)
            .ToListAsync();

        var stillActive = orders.Any(o => ActiveStatuses.Contains(o.Status) || o.Status == OrderStatus.InDelivery);
        if (orders.Count == 0 || (!stillActive && orders.Max(o => o.UpdatedAt) < DateTime.UtcNow - TrackingValidity))
            throw new KeyNotFoundException("Link za praćenje nije ispravan ili je istekao.");

        return orders;
    }

    private static string NewTrackingCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    // Stranicu za praćenje može da otvori svako ko ima link, pa adresa i telefon nisu prikazani u celosti
    private static OrderDto MapToTrackingDto(Order o)
    {
        var dto = MapToDto(o);
        dto.DeliveryAddress = MaskAddress(o.DeliveryAddress);
        dto.ContactPhone = MaskPhone(o.ContactPhone);
        return dto;
    }

    private static string? MaskAddress(string? address) =>
        address == null ? null : Regex.Replace(address, @"\S*\d\S*", "•••");

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return phone;
        var digits = phone.Count(char.IsDigit);
        var seen = 0;
        return new string(phone.Select(c => char.IsDigit(c) && ++seen <= digits - 2 ? '•' : c).ToArray());
    }

    public async Task<int> GetVendorPendingCount(int vendorId)
    {
        return await _context.Orders
            .CountAsync(o => o.VendorId == vendorId && o.Status == OrderStatus.Pending);
    }

    public async Task<List<OrderDto>> GetAvailableCourierOrders()
    {
        var orders = await OrdersWithDetails()
            .Where(o => o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && ActiveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync();

        return orders.Select(MapToCourierPreview).ToList();
    }

    public async Task<List<OrderDto>> GetCourierOrders(int courierId)
    {
        return await OrdersWithDetails()
            .Where(o => o.CourierId == courierId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .Select(o => MapToDto(o))
            .ToListAsync();
    }

    public async Task<OrderDto> AcceptCourierOrder(int orderId, int courierId)
    {
        if (!await _context.CourierProfiles.AnyAsync(c => c.UserId == courierId && c.IsOnShift))
            throw new UnauthorizedAccessException("Uključite smenu da biste prihvatali porudžbine.");

        var now = DateTime.UtcNow;

        // Jedan uslovni UPDATE: od više kurira koji istovremeno prihvate, upis uspeva samo prvom
        var updated = await _context.Orders
            .Where(o => o.Id == orderId && o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && ActiveStatuses.Contains(o.Status))
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.CourierId, (int?)courierId)
                .SetProperty(o => o.CourierAssignedAt, (DateTime?)now)
                .SetProperty(o => o.UpdatedAt, now));

        if (updated == 0)
        {
            if (!await _context.Orders.AnyAsync(o => o.Id == orderId))
                throw new KeyNotFoundException("Porudžbina nije pronađena.");
            throw new InvalidOperationException("Porudžbinu je već preuzeo drugi kurir ili više nije dostupna.");
        }

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
                "Kurir je preuzeo vašu porudžbinu", $"{courierName} će vam dostaviti porudžbinu od {order.Vendor.Name}.", $"/order-detail/{order.Id}");

        return MapToDto(order);
    }

    public async Task<OrderDto> MarkPickedUp(int orderId, int courierId)
    {
        var order = await LoadCourierOrder(orderId, courierId);
        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.ReadyForPickup))
            throw new InvalidOperationException("Robu možete preuzeti tek kada prodavac potvrdi porudžbinu.");

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
            throw new InvalidOperationException("Isporuku možete potvrditi tek nakon što preuzmete robu.");

        order.Status = OrderStatus.Completed;
        order.DeliveredAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyCourier: false);
        return MapToDto(order);
    }

    private async Task<Order> LoadCourierOrder(int orderId, int courierId) =>
        await OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == orderId && o.CourierId == courierId)
        ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

    private Task<List<int>> VendorUserIds(int vendorId) =>
        _context.VendorUsers.Where(vu => vu.VendorId == vendorId).Select(vu => vu.UserId).ToListAsync();

    // Javlja promenu statusa kupcu, prodavcu i dodeljenom kuriru (SignalR + push kupcu)
    private async Task NotifyStatusChanged(Order order, bool notifyCustomer = true, bool notifyVendor = true, bool notifyCourier = true)
    {
        // Poruka prodavca ide samo uz promenu uz koju je napisana, ne uz svaku sledeću
        var freshMessage = order.VendorMessageAt.HasValue && order.VendorMessageAt.Value >= order.UpdatedAt
            ? order.VendorMessage
            : null;
        var payload = new { orderId = order.Id, status = order.Status.ToString(), vendorName = order.Vendor.Name, message = freshMessage };

        if (notifyCustomer && order.TrackingCode != null)
            await _trackingHub.Clients.Group(TrackingHub.GroupName(order.TrackingCode)).SendAsync("OrderStatusChanged", payload);

        if (notifyCustomer && order.CustomerId.HasValue)
        {
            await _hubContext.Clients.Group($"user_{order.CustomerId.Value}").SendAsync("OrderStatusChanged", payload);

            var pickup = order.Fulfillment == FulfillmentType.Pickup;
            var text = order.Status switch
            {
                OrderStatus.Confirmed => pickup ? "Prodavac je potvrdio rezervaciju." : "Prodavac je potvrdio porudžbinu.",
                OrderStatus.Rejected => pickup ? "Prodavac je odbio rezervaciju." : "Prodavac je odbio porudžbinu.",
                OrderStatus.ReadyForPickup => pickup ? "Roba je spremna, možete doći po nju." : "Porudžbina je spremna i čeka kurira.",
                OrderStatus.InDelivery => "Kurir je preuzeo robu i krenuo ka vama.",
                OrderStatus.Completed => pickup ? "Rezervacija je preuzeta. Hvala!" : "Porudžbina je isporučena. Prijatno!",
                _ => null
            };
            if (text != null && freshMessage != null)
                text += $" Poruka prodavca: {freshMessage}";
            if (text != null)
                await _pushService.SendToUsers(new[] { order.CustomerId.Value },
                    $"Porudžbina #{order.Id} – {order.Vendor.Name}", text, $"/order-detail/{order.Id}");
        }

        if (notifyVendor)
            await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("OrderStatusChanged", payload);

        // Dodeljeni kurir prati status (npr. "Spremno" znači da može po robu)
        if (notifyCourier && order.CourierId.HasValue)
        {
            await _hubContext.Clients.Group($"user_{order.CourierId.Value}").SendAsync("OrderStatusChanged", payload);
            if (order.Status is OrderStatus.ReadyForPickup or OrderStatus.Cancelled or OrderStatus.Rejected)
                await _pushService.SendToUsers(new[] { order.CourierId.Value },
                    $"Porudžbina #{order.Id}",
                    order.Status == OrderStatus.ReadyForPickup ? $"Roba je spremna kod: {order.Vendor.Name}." : "Porudžbina je otkazana.",
                    "/courier-orders");
        }
    }

    private IQueryable<Order> OrdersWithDetails() => _context.Orders
        .Include(o => o.Items)
        .Include(o => o.Vendor).ThenInclude(v => v.Market)
        .Include(o => o.Vendor).ThenInclude(v => v.Stall)
        .Include(o => o.Customer)
        .Include(o => o.Courier);

    private async Task NotifyNewOrders(List<Order> orders)
    {
        foreach (var order in orders)
        {
            var customerName = order.Customer != null
                ? $"{order.Customer.FirstName} {order.Customer.LastName}"
                : order.GuestName ?? "Gost";

            await _hubContext.Clients.Group($"vendor_{order.VendorId}")
                .SendAsync("NewOrder", new
                {
                    orderId = order.Id,
                    customerName,
                    totalAmount = order.TotalAmount,
                    itemCount = order.Items.Count,
                    fulfillment = order.Fulfillment.ToString()
                });

            await _pushService.SendToUsers(await VendorUserIds(order.VendorId),
                order.Fulfillment == FulfillmentType.Pickup ? $"Nova rezervacija #{order.Id}" : $"Nova porudžbina #{order.Id} (Eko kurir)",
                $"{customerName} · {order.TotalAmount:0} din", "/vendor-orders");

            if (order.Fulfillment != FulfillmentType.Courier) continue;

            // Obaveštenje dobijaju samo kuriri koji su na smeni
            var onShift = await _context.CourierProfiles
                .Where(c => c.IsOnShift && c.User.IsActive)
                .Select(c => c.UserId)
                .ToListAsync();
            if (onShift.Count == 0) continue;

            await _hubContext.Clients.Groups(onShift.Select(id => $"user_{id}").ToList())
                .SendAsync("CourierNewOrder", MapToCourierPreview(order));
            await _pushService.SendToUsers(onShift,
                "Nova porudžbina za dostavu",
                $"{order.Vendor.Name} ({order.Vendor.Market?.Name}) → {order.DeliveryAddress}",
                "/courier-orders");
        }
    }

    private Task NotifyCouriersOrderUnavailable(int orderId) =>
        _hubContext.Clients.Group(CouriersGroup).SendAsync("CourierOrderUnavailable", new { orderId });

    // Pre prihvatanja kurir vidi odakle i kuda se nosi, ali ne i ime i telefon kupca
    private static OrderDto MapToCourierPreview(Order o)
    {
        var dto = MapToDto(o);
        dto.CustomerName = string.Empty;
        dto.ContactPhone = null;
        dto.CustomerId = null;
        return dto;
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus next)
    {
        var valid = (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Rejected) => true,
            (OrderStatus.Confirmed, OrderStatus.ReadyForPickup) => true,
            (OrderStatus.ReadyForPickup, OrderStatus.Completed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException(
                $"Nije moguć prelaz statusa iz '{current}' u '{next}'.");
    }

    private static OrderDto MapToDto(Order o) => new()
    {
        Id = o.Id,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer != null
            ? $"{o.Customer.FirstName} {o.Customer.LastName}"
            : o.GuestName ?? "Gost",
        VendorId = o.VendorId,
        VendorName = o.Vendor.Name,
        Status = o.Status,
        TotalAmount = o.TotalAmount,
        Fulfillment = o.Fulfillment,
        DeliveryFee = o.DeliveryFee,
        AmountToPay = o.TotalAmount + o.DeliveryFee,
        VendorMessage = o.VendorMessage,
        VendorMessageAt = o.VendorMessageAt,
        Note = o.Note,
        CreatedAt = o.CreatedAt,
        IsGuestOrder = o.CustomerId == null,
        ContactPhone = o.ContactPhone,
        DeliveryAddress = o.DeliveryAddress,
        MarketName = o.Vendor.Market?.Name,
        StallLabel = o.Vendor.Stall?.Label,
        VendorPhones = o.Vendor.Phones,
        CourierId = o.CourierId,
        CourierName = o.Courier != null ? $"{o.Courier.FirstName} {o.Courier.LastName}".Trim() : null,
        CourierPhone = o.Courier?.PhoneNumber,
        CourierAssignedAt = o.CourierAssignedAt,
        PickedUpAt = o.PickedUpAt,
        DeliveredAt = o.DeliveredAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            Unit = i.Unit,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice
        }).ToList()
    };
}
