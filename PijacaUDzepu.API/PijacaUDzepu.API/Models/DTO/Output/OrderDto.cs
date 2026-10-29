using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Output;

public class OrderDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public FulfillmentType Fulfillment { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal AmountToPay { get; set; }
    public string? VendorMessage { get; set; }
    public DateTime? VendorMessageAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsGuestOrder { get; set; }
    public string? TrackingCode { get; set; }
    public string? ContactPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    // Mesto preuzimanja robe za kurira
    public string? MarketName { get; set; }
    public string? StallLabel { get; set; }
    public List<string> VendorPhones { get; set; } = new();

    public int? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
    public DateTime? CourierAssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public ProductUnit Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
