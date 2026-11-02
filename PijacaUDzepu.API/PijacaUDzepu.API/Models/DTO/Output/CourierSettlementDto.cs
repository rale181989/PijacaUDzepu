namespace PijacaUDzepu.API.Models.DTO.Output;

// Dnevni obračun kurira: šta je naplatio od kupaca, koliko duguje kom prodavcu (roba) i koliko je zaradio (dostava)
public class CourierSettlementDto
{
    public DateOnly Date { get; set; }
    public int Delivered { get; set; }
    public int InProgress { get; set; }
    public decimal Collected { get; set; }
    public decimal OwedToVendors { get; set; }
    public decimal Earned { get; set; }
    public List<VendorSettlementDto> Vendors { get; set; } = new();
    public List<SettlementDeliveryDto> Deliveries { get; set; } = new();
}

public class VendorSettlementDto
{
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? MarketName { get; set; }
    public int Orders { get; set; }
    public decimal Amount { get; set; }
}

public class SettlementDeliveryDto
{
    public int OrderId { get; set; }
    public DateTime DeliveredAt { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public decimal Goods { get; set; }
    public decimal Fee { get; set; }
}
