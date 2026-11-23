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

public partial class OrderService : IOrderService
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
        AutoRejectMinutes = Math.Max(1, config.GetValue("Orders:AutoRejectMinutes", 15));
    }

    // Link za praćenje važi dok je porudžbina u toku i još 30 dana posle poslednje promene
    private static readonly TimeSpan TrackingValidity = TimeSpan.FromDays(30);

    // Cena kurira po korpi, tj. po pijaci (korpa je uvek sa jedne pijace), bez obzira na broj tezgi;
    // podešava se sa Delivery__CourierFee. Naplaćuje se na jednoj porudžbini korpe (MoveDeliveryFee).
    public decimal CourierFee => _config.GetValue<decimal?>("Delivery:CourierFee") ?? 300m;

    // Porudžbine u toku pre preuzimanja robe (na čekanju, potvrđene, spremne); bez "U dostavi"
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup };

    private const decimal MaxOrderAmount = 1_000_000m;

    // Kurir vidi i prihvata porudžbinu tek kada je prodavac potvrdi (tok: kupac → prodavac → kurir)
    private static readonly OrderStatus[] CourierOfferStatuses =
        { OrderStatus.Confirmed, OrderStatus.ReadyForPickup };

    // Tab "Aktivne" kod prodavca i kurira: sve što još nije završeno, odbijeno ili otkazano
    private static readonly OrderStatus[] OpenStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup, OrderStatus.InDelivery };

    private const string CouriersGroup = "couriers";

    // Porudžbina na koju prodavac ne odgovori za Orders:AutoRejectMinutes (podrazumevano 15) sama se odbija
    // (OrderWatchdog), da kupac ne čeka i da ostale porudžbine iste korpe krenu ka kuriru
    public static int AutoRejectMinutes { get; private set; } = 15;
    public const string UnansweredReason = "Prodavac nije odgovorio na vreme, pa je porudžbina otkazana.";
    private const int AutoRejectWindowHours = 2;

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

        // Ime kupca za obaveštenje prodavcu i odgovor (inače bi pisalo "Gost")
        var customer = await _context.Users.FindAsync(customerId);
        foreach (var order in orders) order.Customer = customer;

        await NotifyNewOrders(orders);

        return orders.Select(o => ForCustomer(MapToDto(o))).ToList();
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
            var dto = ForCustomer(MapToDto(o));
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
        // Isti proizvod dva puta bi zaobišao granicu količine po stavci; korpa ga ionako šalje jednom
        if (productIds.Distinct().Count() != productIds.Count)
            throw new InvalidOperationException(L.T("Isti proizvod je više puta u porudžbini. Osvežite korpu i poručite ponovo."));
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

        // Jedna porudžbina = jedna pijaca: kurir ne obilazi više pijaca, a kupac ne preuzima na više mesta
        if (products.Select(p => p.Vendor.MarketId).Distinct().Count() > 1)
        {
            // Pijaca prve stavke ostaje, ostale se nabrajaju kao "druge"
            var firstMarket = products.First(p => p.Id == items[0].ProductId).Vendor.MarketId;
            throw new InvalidOperationException(
                L.T("Jedna porudžbina može biti samo sa jedne pijace (zbog dostave i preuzimanja). Uklonite iz korpe robu sa druge pijace: {0}.",
                    string.Join(", ", products.Where(p => p.Vendor.MarketId != firstMarket).Select(p => p.Vendor.Market.Name).Distinct())));
        }

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
                // Usluga jednom po korpi: nosi je prva porudžbina, ostale tezge iz iste korpe su bez nje
                DeliveryFee = fulfillment == FulfillmentType.Courier && orders.Count == 0 ? CourierFee : 0,
                Items = new List<OrderItem>()
            };

            foreach (var item in group)
            {
                var product = products.First(p => p.Id == item.ProductId);

                // Komad, veza, pakovanje...: samo celi brojevi (decimale samo za kg i litar)
                if (product.Unit is not (ProductUnit.Kg or ProductUnit.Litar) && item.Quantity != decimal.Truncate(item.Quantity))
                    throw new InvalidOperationException(L.T("{0}: količina mora biti ceo broj.", product.Name));

                // Kupac je u korpi video drugu cenu: ne naplaćuje se ništa što nije video
                if (item.ExpectedUnitPrice.HasValue && item.ExpectedUnitPrice.Value != product.Price)
                    throw new InvalidOperationException(L.T("Cena za {0} je promenjena: sada je {1} din. Proverite korpu i poručite ponovo.",
                        product.Name, product.Price.ToString("0.##")));

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
            // Iznos u bazi ima najviše 8 cifara pre zareza; ovolika porudžbina sa pijace nije stvarna
            if (order.TotalAmount > MaxOrderAmount)
                throw new InvalidOperationException(L.T("Iznos porudžbine je prevelik. Za velike količine javite se prodavcu."));
            orders.Add(order);
        }

        return orders;
    }

    public async Task<OrderDto> GetOrderById(int orderId)
    {
        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException(L.T("Porudžbina nije pronađena."));

        return MapToDto(order);
    }

    private IQueryable<Order> OrdersWithDetails() => _context.Orders
        .Include(o => o.Items)
        .Include(o => o.Rating)
        .Include(o => o.Vendor).ThenInclude(v => v.Market)
        .Include(o => o.Vendor).ThenInclude(v => v.Stall)
        .Include(o => o.Customer)
        .Include(o => o.Courier)
        .Include(o => o.ClosedByAdmin);

    private Task<List<int>> VendorUserIds(int vendorId) =>
        _context.VendorUsers.Where(vu => vu.VendorId == vendorId).Select(vu => vu.UserId).ToListAsync();

    // Status se upisuje samo ako je u bazi i dalje onaj koji je učitan. Kada dva upisa stignu istovremeno (npr. prodavac
    // potvrdi baš kada server automatski odbija), drugi dobija grešku umesto da pregazi prvi i ponovo skine stanje.
    private async Task ClaimStatus(Order order, OrderStatus expected)
    {
        var claimed = await _context.Orders
            .Where(o => o.Id == order.Id && o.Status == expected)
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.Status, order.Status)
                .SetProperty(o => o.UpdatedAt, order.UpdatedAt));
        if (claimed == 0)
        {
            var current = await _context.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).FirstAsync();
            throw new InvalidOperationException(
                L.T("Nije moguć prelaz statusa iz '{0}' u '{1}'.", StatusName(current), StatusName(order.Status)));
        }
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus next)
    {
        var valid = (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Rejected) => true,
            (OrderStatus.Confirmed, OrderStatus.ReadyForPickup) => true,
            (OrderStatus.ReadyForPickup, OrderStatus.Completed) => true,
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
}
