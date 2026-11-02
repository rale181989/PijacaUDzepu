using PijacaUDzepu.API.Helpers;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Izveštaji se računaju iz porudžbina napravljenih u periodu (po beogradskom datumu).
// Baza vraća samo potrebne kolone, a grupisanje ide u memoriji - za obim jedne pijace to je dovoljno brzo.
public class ReportService : IReportService
{
    private static readonly TimeZoneInfo LocalZone = LocalTime.Zone;

    private readonly DataContext _context;

    public ReportService(DataContext context)
    {
        _context = context;
    }

    public async Task<DateOnly> GetFirstOrderDate()
    {
        var first = await _context.Orders.MinAsync(o => (DateTime?)o.CreatedAt);
        return ToLocalDate(first ?? DateTime.UtcNow);
    }

    public async Task<OrdersReportDto> GetOrdersReport(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var orders = await _context.Orders
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new { o.Status, o.Fulfillment, o.TotalAmount, o.DeliveryFee, o.CreatedAt })
            .ToListAsync();

        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var goods = completed.Sum(o => o.TotalAmount);

        return new OrdersReportDto
        {
            From = from,
            To = to,
            TotalOrders = orders.Count,
            CompletedOrders = completed.Count,
            RejectedOrders = orders.Count(o => o.Status == OrderStatus.Rejected),
            CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
            ActiveOrders = orders.Count(o => IsActive(o.Status)),
            GoodsTotal = goods,
            DeliveryFeesTotal = completed.Sum(o => o.DeliveryFee),
            AverageOrderValue = completed.Count > 0 ? Math.Round(goods / completed.Count, 0) : 0,
            ByStatus = orders
                .GroupBy(o => o.Status)
                .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
                .OrderByDescending(s => s.Count)
                .ToList(),
            ByFulfillment = orders
                .GroupBy(o => o.Fulfillment)
                .Select(g => new FulfillmentReportDto
                {
                    Fulfillment = g.Key,
                    Orders = g.Count(),
                    Completed = g.Count(o => o.Status == OrderStatus.Completed),
                    GoodsTotal = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                    DeliveryFeesTotal = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.DeliveryFee)
                })
                .ToList(),
            ByDay = orders
                .GroupBy(o => ToLocalDate(o.CreatedAt))
                .Select(g => new DayReportDto
                {
                    Date = g.Key,
                    Orders = g.Count(),
                    Completed = g.Count(o => o.Status == OrderStatus.Completed),
                    GoodsTotal = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount)
                })
                .OrderByDescending(d => d.Date)
                .ToList()
        };
    }

    public async Task<List<CustomerReportDto>> GetCustomersReport(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var orders = await _context.Orders
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new
            {
                o.CustomerId,
                FirstName = o.Customer != null ? o.Customer.FirstName : null,
                LastName = o.Customer != null ? o.Customer.LastName : null,
                UserName = o.Customer != null ? o.Customer.UserName : null,
                UserPhone = o.Customer != null ? o.Customer.PhoneNumber : null,
                o.GuestName,
                o.ContactPhone,
                o.Status,
                o.TotalAmount,
                o.CreatedAt
            })
            .ToListAsync();

        return orders
            .GroupBy(o => o.CustomerId.HasValue ? $"user-{o.CustomerId}" : $"guest-{PhoneKey(o.ContactPhone) ?? o.GuestName?.Trim().ToLowerInvariant()}")
            .Select(g =>
            {
                var last = g.MaxBy(o => o.CreatedAt)!;
                var isGuest = last.CustomerId == null;
                var name = isGuest
                    ? last.GuestName?.Trim()
                    : $"{last.FirstName} {last.LastName}".Trim();
                return new CustomerReportDto
                {
                    Key = g.Key,
                    Name = string.IsNullOrWhiteSpace(name) ? (last.UserName ?? "Gost") : name,
                    UserName = last.UserName,
                    Phone = isGuest ? last.ContactPhone : (last.UserPhone ?? last.ContactPhone),
                    IsGuest = isGuest,
                    Orders = g.Count(),
                    Completed = g.Count(o => o.Status == OrderStatus.Completed),
                    Rejected = g.Count(o => o.Status == OrderStatus.Rejected),
                    GoodsTotal = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                    LastOrderAt = last.CreatedAt
                };
            })
            .OrderByDescending(c => c.Orders)
            .ThenByDescending(c => c.GoodsTotal)
            .ToList();
    }

    public async Task<List<CourierReportDto>> GetCouriersReport(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);

        // Svi kuriri, i oni bez vožnji u periodu
        var couriers = await _context.CourierProfiles
            .Select(c => new
            {
                c.UserId, c.User.FirstName, c.User.LastName, c.User.UserName, c.User.PhoneNumber,
                c.Vehicle, c.LicensePlate, c.IsOnShift
            })
            .ToListAsync();

        var deliveries = await _context.Orders
            .Where(o => o.CourierId != null && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new
            {
                CourierId = o.CourierId!.Value,
                o.Id,
                o.Status,
                o.CreatedAt,
                o.PickedUpAt,
                o.DeliveredAt,
                VendorName = o.Vendor.Name,
                MarketName = o.Vendor.Market != null ? o.Vendor.Market.Name : null,
                StallLabel = o.Vendor.Stall != null ? o.Vendor.Stall.Label : null,
                o.DeliveryAddress,
                o.DeliveryFee,
                o.TotalAmount
            })
            .ToListAsync();

        var byCourier = deliveries.ToLookup(d => d.CourierId);

        return couriers
            .Select(c =>
            {
                var mine = byCourier[c.UserId].OrderByDescending(d => d.CreatedAt).ToList();
                var delivered = mine.Where(d => d.Status == OrderStatus.Completed).ToList();
                var durations = delivered
                    .Where(d => d.PickedUpAt.HasValue && d.DeliveredAt.HasValue)
                    .Select(d => (d.DeliveredAt!.Value - d.PickedUpAt!.Value).TotalMinutes)
                    .ToList();
                var name = $"{c.FirstName} {c.LastName}".Trim();

                return new CourierReportDto
                {
                    CourierId = c.UserId,
                    Name = string.IsNullOrWhiteSpace(name) ? c.UserName ?? $"Kurir #{c.UserId}" : name,
                    Phone = c.PhoneNumber,
                    Vehicle = c.Vehicle,
                    LicensePlate = c.LicensePlate,
                    IsOnShift = c.IsOnShift,
                    Delivered = delivered.Count,
                    InProgress = mine.Count(d => IsActive(d.Status)),
                    FeesTotal = delivered.Sum(d => d.DeliveryFee),
                    CashCollected = delivered.Sum(d => d.TotalAmount + d.DeliveryFee),
                    AverageDeliveryMinutes = durations.Count > 0 ? Math.Round(durations.Average(), 0) : null,
                    Markets = mine
                        .GroupBy(d => d.MarketName ?? "Bez pijace")
                        .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
                        .OrderByDescending(m => m.Count)
                        .ToList(),
                    Deliveries = mine.Select(d => new CourierDeliveryDto
                    {
                        OrderId = d.Id,
                        Status = d.Status,
                        CreatedAt = d.CreatedAt,
                        PickedUpAt = d.PickedUpAt,
                        DeliveredAt = d.DeliveredAt,
                        VendorName = d.VendorName,
                        MarketName = d.MarketName,
                        StallLabel = d.StallLabel,
                        DeliveryAddress = d.DeliveryAddress,
                        DeliveryFee = d.DeliveryFee,
                        AmountToPay = d.TotalAmount + d.DeliveryFee,
                        Minutes = d.PickedUpAt.HasValue && d.DeliveredAt.HasValue
                            ? Math.Round((d.DeliveredAt.Value - d.PickedUpAt.Value).TotalMinutes, 0)
                            : null
                    }).ToList()
                };
            })
            .OrderByDescending(c => c.Delivered)
            .ThenByDescending(c => c.InProgress)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<List<VendorReportDto>> GetVendorsReport(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var vendors = await _context.Vendors
            .Select(v => new { v.Id, v.Name, MarketName = v.Market.Name, v.IsActive })
            .ToListAsync();
        var orders = await _context.Orders
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .Select(o => new
            {
                o.VendorId, o.Status, o.TotalAmount, o.CreatedAt, o.RespondedAt, o.VendorMessage, o.UpdatedAt,
                Score = o.Rating != null ? (int?)o.Rating.VendorScore : null
            })
            .ToListAsync();
        var byVendor = orders.ToLookup(o => o.VendorId);

        return vendors.Select(v =>
        {
            var mine = byVendor[v.Id].ToList();
            var responses = mine.Where(o => o.RespondedAt != null).Select(o => (o.RespondedAt!.Value - o.CreatedAt).TotalMinutes).ToList();
            var scores = mine.Where(o => o.Score != null).Select(o => o.Score!.Value).ToList();
            var rejected = mine.Where(o => o.Status == OrderStatus.Rejected).ToList();
            return new VendorReportDto
            {
                VendorId = v.Id,
                Name = v.Name,
                MarketName = v.MarketName,
                IsActive = v.IsActive,
                Orders = mine.Count,
                Completed = mine.Count(o => o.Status == OrderStatus.Completed),
                Rejected = rejected.Count,
                RejectionRate = mine.Count > 0 ? Math.Round(100.0 * rejected.Count / mine.Count, 1) : 0,
                GoodsTotal = mine.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                AverageResponseMinutes = responses.Count > 0 ? Math.Round(responses.Average(), 1) : null,
                AverageRating = scores.Count > 0 ? Math.Round(scores.Average(), 1) : null,
                RatingCount = scores.Count,
                RecentRejectionReasons = rejected
                    .Where(o => !string.IsNullOrWhiteSpace(o.VendorMessage))
                    .OrderByDescending(o => o.UpdatedAt)
                    .Select(o => o.VendorMessage!)
                    .Take(3)
                    .ToList()
            };
        })
        .OrderByDescending(v => v.GoodsTotal)
        .ThenByDescending(v => v.Orders)
        .ToList();
    }

    // Prodaja po proizvodu i grupi (samo završene porudžbine)
    public async Task<ProductsReportDto> GetProductsReport(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var items = await _context.OrderItems
            .Where(i => i.Order.Status == OrderStatus.Completed && i.Order.CreatedAt >= fromUtc && i.Order.CreatedAt < toUtc)
            .Select(i => new
            {
                i.ProductName, i.Unit, i.Quantity, i.TotalPrice, i.OrderId,
                VendorName = i.Order.Vendor.Name,
                Category = i.Product != null ? (ProductCategory?)i.Product.Category : null
            })
            .ToListAsync();

        return new ProductsReportDto
        {
            Products = items
                .GroupBy(i => new { i.ProductName, i.VendorName, i.Unit })
                .Select(g => new ProductSalesDto
                {
                    ProductName = g.Key.ProductName,
                    VendorName = g.Key.VendorName,
                    Unit = g.Key.Unit,
                    Quantity = g.Sum(i => i.Quantity),
                    Orders = g.Select(i => i.OrderId).Distinct().Count(),
                    Revenue = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(p => p.Revenue)
                .ToList(),
            Categories = items
                .GroupBy(i => i.Category)
                .Select(g => new CategorySalesDto
                {
                    Category = g.Key,
                    Orders = g.Select(i => i.OrderId).Distinct().Count(),
                    Revenue = g.Sum(i => i.TotalPrice)
                })
                .OrderByDescending(c => c.Revenue)
                .ToList()
        };
    }

    // Porudžbine koje trenutno čekaju prodavca ili kurira duže od 15 minuta
    public async Task<List<WaitingOrderDto>> GetWaitingOrders()
    {
        var before = DateTime.UtcNow.AddMinutes(-15);
        var orders = await _context.Orders
            .Where(o => o.CreatedAt < before && (o.Status == OrderStatus.Pending ||
                (o.Fulfillment == FulfillmentType.Courier && o.CourierId == null &&
                 (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.ReadyForPickup))))
            .OrderBy(o => o.CreatedAt)
            .Select(o => new { o.Id, VendorName = o.Vendor.Name, o.Status, o.Fulfillment, o.CreatedAt })
            .Take(100)
            .ToListAsync();
        var now = DateTime.UtcNow;
        return orders.Select(o => new WaitingOrderDto
        {
            OrderId = o.Id,
            VendorName = o.VendorName,
            Status = o.Status,
            Fulfillment = o.Fulfillment,
            CreatedAt = o.CreatedAt,
            Minutes = (int)(now - o.CreatedAt).TotalMinutes,
            Reason = o.Status == OrderStatus.Pending ? "Čeka prodavca" : "Čeka kurira"
        }).ToList();
    }

    private static bool IsActive(OrderStatus s) =>
        s is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.ReadyForPickup or OrderStatus.InDelivery;

    // Gosti se prepoznaju po poslednjih 9 cifara telefona (0641234567 i +381641234567 su isti kupac)
    private static string? PhoneKey(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    private static (DateTime fromUtc, DateTime toUtc) ToUtcRange(DateOnly from, DateOnly to) => (
        TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), LocalZone),
        TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), LocalZone));

    private static DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), LocalZone));

}
