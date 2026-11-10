using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Output;

// Pregled dana za prodavca: šta treba spremiti (zbir stavki svih porudžbina u toku) i današnji promet
public class VendorTodayDto
{
    public int Pending { get; set; }
    public int Confirmed { get; set; }
    public int Ready { get; set; }
    public int InDelivery { get; set; }
    // Prihvaćeno, a još nije spakovano
    public List<PrepareItemDto> ToPrepare { get; set; } = new();
    // Porudžbine koje još čekaju odgovor prodavca (mogu biti odbijene)
    public List<PrepareItemDto> AwaitingAnswer { get; set; } = new();
    public int TodayOrders { get; set; }
    public int TodayCompleted { get; set; }
    public decimal TodayGoodsTotal { get; set; }
    public List<LowStockDto> LowStock { get; set; } = new();
}

public class PrepareItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }
    public decimal Quantity { get; set; }
    public int Orders { get; set; }
}

public class LowStockDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductUnit Unit { get; set; }
    public decimal StockQuantity { get; set; }
}
