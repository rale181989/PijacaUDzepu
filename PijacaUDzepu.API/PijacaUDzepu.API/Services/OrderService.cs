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

public class OrderService : IOrderService
{
    private readonly DataContext _context;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly IHubContext<TrackingHub> _trackingHub;
    private readonly IPushService _pushService;
    private readonly IFavoriteService _favorites;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderService> _logger;
    private readonly IConfiguration _config;

    public OrderService(DataContext context, IHubContext<OrderHub> hubContext, IHubContext<TrackingHub> trackingHub,
        IPushService pushService, IEmailService emailService, ILogger<OrderService> logger, IConfiguration config, IFavoriteService favorites)
    {
        _emailService = emailService;
        _logger = logger;
        _context = context;
        _hubContext = hubContext;
        _trackingHub = trackingHub;
        _pushService = pushService;
        _favorites = favorites;
        _config = config;
    }

    // Link za praćenje važi dok je porudžbina u toku i još 30 dana posle poslednje promene
    private static readonly TimeSpan TrackingValidity = TimeSpan.FromDays(30);

    // Cena Eko kurira po porudžbini (po prodavcu); podešava se sa Delivery__CourierFee
    public decimal CourierFee => _config.GetValue<decimal?>("Delivery:CourierFee") ?? 300m;

    // Porudžbine u toku pre preuzimanja robe (na čekanju, potvrđene, spremne); bez "U dostavi"
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup };

    // Kurir vidi i prihvata porudžbinu tek kada je prodavac potvrdi (tok: kupac → prodavac → kurir)
    private static readonly OrderStatus[] CourierOfferStatuses =
        { OrderStatus.Confirmed, OrderStatus.ReadyForPickup };

    // Tab "Aktivne" kod prodavca i kurira: sve što još nije završeno, odbijeno ili otkazano
    private static readonly OrderStatus[] OpenStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup, OrderStatus.InDelivery };

    private const string CouriersGroup = "couriers";

    // Kupci i gosti privremeno ne mogu da otkažu porudžbinu (prodavac i dalje može da je odbije).
    // Za ponovno uključivanje promeniti i CUSTOMER_CANCEL_ENABLED u klijentu (order.model.ts).
    private static readonly bool CustomerCancelEnabled = false;
    private const string CancelDisabledMessage = "Otkazivanje porudžbina trenutno nije moguće. Javite se prodavcu.";

    public async Task<List<OrderDto>> CreateOrders(int customerId, OrderInputDto dto)
    {
        var address = ValidateAddress(dto.Fulfillment, dto.DeliveryAddress);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var orders = await BuildOrders(dto.Items, dto.Note, dto.Fulfillment);
        await ReserveStock(dto.Items);
        foreach (var order in orders)
        {
            order.CustomerId = customerId;
            order.ContactPhone = dto.ContactPhone.Trim();
            order.DeliveryAddress = address;
        }

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        await NotifyNewOrders(orders);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderDto>> CreateGuestOrders(GuestOrderInputDto dto)
    {
        var address = ValidateAddress(dto.Fulfillment, dto.GuestAddress);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var orders = await BuildOrders(dto.Items, dto.Note, dto.Fulfillment);
        await ReserveStock(dto.Items);
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
        await transaction.CommitAsync();

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

    private void SendRejectionEmailInBackground(string email, Order order)
    {
        var emailService = _emailService;
        var logger = _logger;
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendOrderRejected(email, order);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Slanje obaveštenja o odbijenoj porudžbini #{OrderId} na {Email} nije uspelo.", order.Id, email);
            }
        });
    }

    // Proizvodi sa praćenim stanjem: uslovni UPDATE po proizvodu, pa dva kupca ne mogu da kupe isti poslednji
    // kilogram; na nuli proizvod izlazi iz ponude. Poziva se u transakciji sa upisom porudžbine.
    private async Task ReserveStock(List<OrderItemInputDto> items)
    {
        foreach (var group in items.GroupBy(i => i.ProductId))
        {
            var quantity = group.Sum(i => i.Quantity);
            var tracked = await _context.Products
                .Where(p => p.Id == group.Key && p.StockQuantity != null)
                .Select(p => new { p.Name, p.StockQuantity })
                .FirstOrDefaultAsync();
            if (tracked == null) continue;

            var updated = await _context.Products
                .Where(p => p.Id == group.Key && p.StockQuantity >= quantity)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(p => p.IsAvailable, p => p.StockQuantity - quantity > 0)
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity));
            if (updated == 0)
            {
                // Stanje se ponovo čita: možda ga je u međuvremenu smanjila druga porudžbina
                var left = await _context.Products.Where(p => p.Id == group.Key).Select(p => p.StockQuantity).FirstAsync();
                throw new InvalidOperationException(left > 0
                    ? L.T("{0}: na stanju je još samo {1}.", tracked.Name, left.Value.ToString("0.##"))
                    : L.T("{0}: nema više na stanju.", tracked.Name));
            }
        }
    }

    // Odbijena/otkazana porudžbina vraća količinu na stanje. Proizvod koji je izašao iz ponude jer je stanje palo
    // na nulu vraća se u ponudu (ručno isključen proizvod ostaje isključen); kupci koji čekaju dobijaju obaveštenje.
    private async Task RestoreStock(Order order)
    {
        foreach (var group in order.Items.Where(i => i.ProductId != null).GroupBy(i => i.ProductId!.Value))
        {
            var quantity = group.Sum(i => i.Quantity);
            var product = await _context.Products.Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.Id == group.Key && p.StockQuantity != null);
            if (product == null) continue;

            var backInStock = product.StockQuantity == 0 && !product.IsAvailable;
            product.StockQuantity += quantity;
            if (backInStock) product.IsAvailable = true;
            await _context.SaveChangesAsync();
            if (backInStock) await _favorites.NotifyBackInStock(product, product.Vendor.Name);
        }
    }

    // Pregled dana za prodavca
    public async Task<VendorTodayDto> GetVendorToday(int vendorId)
    {
        var today = LocalTime.Today;
        var fromUtc = LocalTime.ToUtc(today.ToDateTime(TimeOnly.MinValue));
        var toUtc = LocalTime.ToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var active = await _context.Orders
            .Where(o => o.VendorId == vendorId && ActiveStatuses.Contains(o.Status))
            .Select(o => new { o.Status, Items = o.Items.Select(i => new { i.ProductName, i.Unit, i.Quantity }) })
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
            // Spremaju se porudžbine koje nisu predate (na čekanju, potvrđene, spremne)
            ToPrepare = active
                .Where(o => o.Status != OrderStatus.InDelivery)
                .SelectMany(o => o.Items.Select(i => (i.ProductName, i.Unit, i.Quantity, Order: o)))
                .GroupBy(x => (x.ProductName, x.Unit))
                .Select(g => new PrepareItemDto
                {
                    ProductName = g.Key.ProductName,
                    Unit = g.Key.Unit,
                    Quantity = g.Sum(x => x.Quantity),
                    Orders = g.Select(x => x.Order).Distinct().Count()
                })
                .OrderBy(x => x.ProductName)
                .ToList(),
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

    private static string? ValidateAddress(FulfillmentType fulfillment, string? address)
    {
        var trimmed = address?.Trim();
        // Oblik adrese se ne proverava (postoje i kratki nazivi ulica, brojevi sa slovom, "bb"...)
        if (fulfillment == FulfillmentType.Courier && string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException(L.T("Za dostavu kurirom unesite adresu."));
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
                L.T("Sledeći proizvodi nisu dostupni: {0}", string.Join(", ", unavailable.Select(p => p.Name))));

        var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missingIds.Any())
            throw new KeyNotFoundException(L.T("Proizvodi sa ID {0} nisu pronađeni.", string.Join(", ", missingIds)));

        // Pauza, neradni dan ili van radnog vremena: porudžbina se ne prima (poruka kaže kad se tezga otvara)
        var closed = products.Select(p => p.Vendor).DistinctBy(v => v.Id)
            .Select(v => (vendor: v, status: VendorAvailability.Evaluate(v)))
            .Where(x => !x.status.IsOpen)
            .ToList();
        if (closed.Any())
            throw new InvalidOperationException(string.Join(" ", closed.Select(x => VendorAvailability.ClosedMessage(x.vendor, x.status))));

        var groupedByVendor = items
            .GroupBy(item => products.First(p => p.Id == item.ProductId).VendorId)
            .ToList();

        var orders = new List<Order>();
        var checkoutId = Guid.NewGuid();

        foreach (var group in groupedByVendor)
        {
            var order = new Order
            {
                VendorId = group.Key,
                Vendor = products.First(p => p.VendorId == group.Key).Vendor,
                CheckoutId = checkoutId,
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

    public async Task<OrderDto> GetOrderById(int orderId)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        return MapToDto(order);
    }

    public async Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus, string? message = null)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.VendorId == vendorId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        ValidateStatusTransition(order.Status, newStatus);
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
        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, notifyVendor: false);
        if (wasPending)
            await StopVendorAlarm(order);
        if (newStatus == OrderStatus.Rejected)
            await RestoreStock(order);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue && newStatus == OrderStatus.Confirmed)
            await NotifyCouriersNewOrder(order);
        // Kuriri koji još biraju vide da je roba spremna
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue && newStatus == OrderStatus.ReadyForPickup)
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

        return MapToDto(order);
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

        var wasPending = order.Status == OrderStatus.Pending;
        order.Status = OrderStatus.Completed;
        order.UpdatedAt = DateTime.UtcNow;
        if (wasPending) order.RespondedAt = DateTime.UtcNow;
        order.ClosedByAdminId = adminId;
        order.ClosedByAdminAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        // Ime admina za trag u odgovoru
        await _context.Entry(order).Reference(o => o.ClosedByAdmin).LoadAsync();

        await NotifyStatusChanged(order, notifyCustomer: false);
        if (wasPending)
            await StopVendorAlarm(order);
        if (order.Fulfillment == FulfillmentType.Courier && !order.CourierId.HasValue)
            await NotifyCouriersOrderUnavailable(order.Id);

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
        return customerId != null ? MapToDto(order) : MapToTrackingDto(order);
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

    // Stranicu za praćenje može da otvori svako ko ima link, pa adresa i telefon nisu prikazani u celosti
    private static OrderDto MapToTrackingDto(Order o)
    {
        var dto = MapToDto(o);
        dto.DeliveryAddress = MaskAddress(o.DeliveryAddress);
        dto.ContactPhone = MaskPhone(o.ContactPhone);
        dto.ClosedByAdminName = null; // gost vidi samo da je zatvorio administrator, ne i ko
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

    public Task<int> GetCustomerActiveCount(int customerId) =>
        _context.Orders.CountAsync(o => o.CustomerId == customerId && OpenStatuses.Contains(o.Status));

    public async Task<int> GetVendorPendingCount(int vendorId)
    {
        return await _context.Orders
            .CountAsync(o => o.VendorId == vendorId && o.Status == OrderStatus.Pending);
    }

    public async Task<List<OrderDto>> GetAvailableCourierOrders(int courierId)
    {
        var orders = await OrdersWithDetails()
            .Where(o => o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && CourierOfferStatuses.Contains(o.Status))
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

    public async Task<OrderDto> AcceptCourierOrder(int orderId, int courierId)
    {
        if (!await _context.CourierProfiles.AnyAsync(c => c.UserId == courierId && c.IsOnShift))
            throw new UnauthorizedAccessException(L.T("Uključite smenu da biste prihvatali porudžbine."));

        var now = DateTime.UtcNow;

        // Jedan uslovni UPDATE: od više kurira koji istovremeno prihvate, upis uspeva samo prvom
        var updated = await _context.Orders
            .Where(o => o.Id == orderId && o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && CourierOfferStatuses.Contains(o.Status))
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.CourierId, (int?)courierId)
                .SetProperty(o => o.CourierAssignedAt, (DateTime?)now)
                .SetProperty(o => o.UpdatedAt, now));

        if (updated == 0)
        {
            if (!await _context.Orders.AnyAsync(o => o.Id == orderId))
                throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));
            throw new InvalidOperationException(L.T("Porudžbinu je već preuzeo drugi kurir ili više nije dostupna."));
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
                "Dodeljen je kurir", $"{courierName} preuzima robu kod {order.Vendor.Name} i donosi vam je.", $"/order-detail/{order.Id}");

        return MapToDto(order);
    }

    public async Task DeclineCourierOrder(int orderId, int courierId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.Fulfillment == FulfillmentType.Courier)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        if (!await _context.CourierDeclines.AnyAsync(d => d.OrderId == orderId && d.CourierId == courierId))
        {
            _context.CourierDeclines.Add(new CourierDecline { OrderId = orderId, CourierId = courierId });
            await _context.SaveChangesAsync();
        }

        // Nestaje sa spiska i prestaje da zvoni samo ovom kuriru (i na njegovim drugim uređajima)
        await _hubContext.Clients.Group($"user_{courierId}").SendAsync("CourierOrderUnavailable", new { orderId = order.Id });
        await _pushService.CancelAlarm(new[] { courierId }, CourierAlarmTag(order.Id));
    }

    // "Prihvati sve": sve slobodne porudžbine iste korpe (isti kupac i adresa, više tezgi).
    // Svaka se prihvata posebno, pa ako je neku u međuvremenu uzeo drugi kurir, ostale se ipak prihvataju.
    public async Task<List<OrderDto>> AcceptCourierCheckout(Guid checkoutId, int courierId)
    {
        var ids = await _context.Orders
            .Where(o => o.CheckoutId == checkoutId && o.Fulfillment == FulfillmentType.Courier && o.CourierId == null && CourierOfferStatuses.Contains(o.Status))
            .Where(o => !_context.CourierDeclines.Any(d => d.OrderId == o.Id && d.CourierId == courierId))
            .Select(o => o.Id)
            .ToListAsync();
        if (ids.Count == 0)
            throw new InvalidOperationException(L.T("Ove porudžbine su već preuzete ili više nisu dostupne."));

        var accepted = new List<OrderDto>();
        foreach (var id in ids)
        {
            try { accepted.Add(await AcceptCourierOrder(id, courierId)); }
            catch (InvalidOperationException) { }
        }
        if (accepted.Count == 0)
            throw new InvalidOperationException(L.T("Ove porudžbine su već preuzete ili više nisu dostupne."));
        return accepted;
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
                o.DeliveryAddress, o.TotalAmount, o.DeliveryFee, DeliveredAt = o.DeliveredAt!.Value
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
                .GroupBy(d => new { d.VendorId, d.VendorName, d.MarketName })
                .Select(g => new VendorSettlementDto
                {
                    VendorId = g.Key.VendorId, VendorName = g.Key.VendorName, MarketName = g.Key.MarketName,
                    Orders = g.Count(), Amount = g.Sum(d => d.TotalAmount)
                })
                .OrderByDescending(v => v.Amount)
                .ToList(),
            Deliveries = delivered.Select(d => new SettlementDeliveryDto
            {
                OrderId = d.Id, DeliveredAt = d.DeliveredAt, VendorName = d.VendorName,
                DeliveryAddress = d.DeliveryAddress, Goods = d.TotalAmount, Fee = d.DeliveryFee
            }).ToList()
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
                OrderStatus.NotDelivered => $"Porudžbina nije dostavljena. Razlog: {order.NotDeliveredReason}",
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
        .Include(o => o.Rating)
        .Include(o => o.Vendor).ThenInclude(v => v.Market)
        .Include(o => o.Vendor).ThenInclude(v => v.Stall)
        .Include(o => o.Customer)
        .Include(o => o.Courier)
        .Include(o => o.ClosedByAdmin);

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

            // Zvoni prodavcu dok porudžbinu ne prihvati ili odbije
            await _pushService.SendAlarm(await VendorUserIds(order.VendorId), VendorAlarmTag(order.Id),
                order.Fulfillment == FulfillmentType.Pickup ? $"Nova rezervacija #{order.Id}" : $"Nova porudžbina #{order.Id} (Eko kurir)",
                $"{customerName} · {order.TotalAmount:0} din", "/vendor-orders");

            // Kuriri dobijaju porudžbinu tek kada je prodavac potvrdi (NotifyCouriersNewOrder)
        }
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

    // Porudžbina više nije na čekanju: prestaje da zvoni prodavcu, i na drugim uređajima/nalozima iste tezge
    private async Task StopVendorAlarm(Order order)
    {
        await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("PendingChanged", new { orderId = order.Id });
        await _pushService.CancelAlarm(await VendorUserIds(order.VendorId), VendorAlarmTag(order.Id));
    }

    private static string VendorAlarmTag(int orderId) => $"order_{orderId}";
    private static string CourierAlarmTag(int orderId) => $"courier_order_{orderId}";

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

        // Najčešće: status je u međuvremenu promenio neko drugi (npr. kurir je već preuzeo robu)
        if (!valid)
            throw new InvalidOperationException(
                L.T("Nije moguć prelaz statusa iz '{0}' u '{1}'.", StatusName(current), StatusName(next)));
    }

    private static string StatusName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => L.T("na čekanju"),
        OrderStatus.Confirmed => L.T("potvrđena"),
        OrderStatus.Rejected => L.T("odbijena"),
        OrderStatus.ReadyForPickup => L.T("spremna"),
        OrderStatus.InDelivery => L.T("u dostavi"),
        OrderStatus.Completed => L.T("završena"),
        OrderStatus.Cancelled => L.T("otkazana"),
        OrderStatus.NotDelivered => L.T("nije dostavljena"),
        _ => status.ToString()
    };

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
        MarketAddress = o.Vendor.Market?.Address,
        CheckoutId = o.CheckoutId,
        StallLabel = o.Vendor.Stall?.Label,
        VendorPhones = o.Vendor.Phones,
        CourierId = o.CourierId,
        CourierName = o.Courier != null ? $"{o.Courier.FirstName} {o.Courier.LastName}".Trim() : null,
        NotDeliveredReason = o.NotDeliveredReason,
        NotDeliveredAt = o.NotDeliveredAt,
        ClosedByAdminName = o.ClosedByAdmin == null ? null
            : $"{o.ClosedByAdmin.FirstName} {o.ClosedByAdmin.LastName}".Trim() is { Length: > 0 } adminName ? adminName : o.ClosedByAdmin.UserName,
        ClosedByAdminAt = o.ClosedByAdminAt,
        CourierPhone = o.Courier?.PhoneNumber,
        CourierAssignedAt = o.CourierAssignedAt,
        RespondedAt = o.RespondedAt,
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
        }).ToList(),
        Rating = o.Rating == null ? null : new OrderRatingDto { VendorScore = o.Rating.VendorScore, CourierScore = o.Rating.CourierScore, Comment = o.Rating.Comment, CreatedAt = o.Rating.CreatedAt }
    };
}
