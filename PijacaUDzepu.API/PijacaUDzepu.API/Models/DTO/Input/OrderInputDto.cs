using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class OrderInputDto
{
    [Required]
    [MinLength(1)]
    public List<OrderItemInputDto> Items { get; set; } = new();
    public string? Note { get; set; }
}

public class GuestOrderInputDto
{
    [Required]
    [MinLength(1)]
    public List<OrderItemInputDto> Items { get; set; } = new();
    public string? Note { get; set; }

    [Required]
    public string GuestName { get; set; } = string.Empty;

    [Required]
    public string GuestPhone { get; set; } = string.Empty;

    public string? GuestAddress { get; set; }
}

public class OrderItemInputDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(0.01, 999999)]
    public decimal Quantity { get; set; }
}
