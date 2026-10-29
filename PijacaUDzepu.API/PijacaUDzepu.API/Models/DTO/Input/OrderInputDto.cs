using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class OrderInputDto
{
    [Required]
    [MinLength(1)]
    public List<OrderItemInputDto> Items { get; set; } = new();
    public string? Note { get; set; }

    [Required]
    public FulfillmentType Fulfillment { get; set; }

    [Required]
    public string ContactPhone { get; set; } = string.Empty;

    // Obavezna samo kada robu nosi kurir
    public string? DeliveryAddress { get; set; }
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

    // Opciono: na ovu adresu se šalje link za praćenje porudžbine
    [EmailAddress]
    [MaxLength(200)]
    public string? GuestEmail { get; set; }

    [Required]
    public FulfillmentType Fulfillment { get; set; }

    // Obavezna samo kada robu nosi kurir
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
