using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Output;

// Izveštaji za SuperAdmin-a. Period je po lokalnom (beogradskom) datumu, uključujući oba dana.
// "Promet" računa samo završene porudžbine.

public class OrdersReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int RejectedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int ActiveOrders { get; set; }
    public decimal GoodsTotal { get; set; }
    public decimal DeliveryFeesTotal { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<StatusCountDto> ByStatus { get; set; } = new();
    public List<FulfillmentReportDto> ByFulfillment { get; set; } = new();
    public List<DayReportDto> ByDay { get; set; } = new();
}

public class StatusCountDto
{
    public OrderStatus Status { get; set; }
    public int Count { get; set; }
}

public class FulfillmentReportDto
{
    public FulfillmentType Fulfillment { get; set; }
    public int Orders { get; set; }
    public int Completed { get; set; }
    public decimal GoodsTotal { get; set; }
    public decimal DeliveryFeesTotal { get; set; }
}

public class DayReportDto
{
    public DateOnly Date { get; set; }
    public int Orders { get; set; }
    public int Completed { get; set; }
    public decimal GoodsTotal { get; set; }
}

public class CustomerReportDto
{
    // Prijavljeni kupac: "user-{id}"; gost: "guest-{telefon}" (gosti se prepoznaju po broju telefona)
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Phone { get; set; }
    public bool IsGuest { get; set; }
    public int Orders { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public decimal GoodsTotal { get; set; }
    public DateTime LastOrderAt { get; set; }
}

public class CourierReportDto
{
    public int CourierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public CourierVehicle? Vehicle { get; set; }
    public string? LicensePlate { get; set; }
    public bool IsOnShift { get; set; }
    public int Delivered { get; set; }
    public int InProgress { get; set; }
    public decimal FeesTotal { get; set; }
    public decimal CashCollected { get; set; }
    public double? AverageDeliveryMinutes { get; set; }
    public List<NameCountDto> Markets { get; set; } = new();
    public List<CourierDeliveryDto> Deliveries { get; set; } = new();
}

public class NameCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CourierDeliveryDto
{
    public int OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? MarketName { get; set; }
    public string? StallLabel { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal AmountToPay { get; set; }
    public double? Minutes { get; set; }
}

public class VendorReportDto
{
    public int VendorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MarketName { get; set; }
    public bool IsActive { get; set; }
    public int Orders { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public double RejectionRate { get; set; }
    public decimal GoodsTotal { get; set; }
    // Prosečno vreme od porudžbine do prvog odgovora prodavca (minuti); null dok nema podataka
    public double? AverageResponseMinutes { get; set; }
    public double? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public List<string> RecentRejectionReasons { get; set; } = new();
}

public class ProductsReportDto
{
    public List<ProductSalesDto> Products { get; set; } = new();
    public List<CategorySalesDto> Categories { get; set; } = new();
}

public class ProductSalesDto
{
    public string ProductName { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }
    public decimal Quantity { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}

public class CategorySalesDto
{
    public ProductCategory? Category { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}

public class WaitingOrderDto
{
    public int OrderId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public FulfillmentType Fulfillment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Minutes { get; set; }
    public string Reason { get; set; } = string.Empty;
}
