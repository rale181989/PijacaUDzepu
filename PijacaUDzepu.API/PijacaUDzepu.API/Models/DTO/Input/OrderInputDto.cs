using System.ComponentModel.DataAnnotations;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Models.DTO.Input;

// Telefon: cifre, razmaci, +, /, -, zagrade i tačka; 6-30 znakova, počinje cifrom ili +, završava se cifrom
// (razmaci oko broja su dozvoljeni, server ih skraćuje)
public static class InputRules
{
    public const string Phone = @"^\s*\+?[0-9][0-9 ()/.\-]{4,28}[0-9]\s*$";
}

public class OrderInputDto
{
    [Required]
    [MinLength(1)]
    public List<OrderItemInputDto> Items { get; set; } = new();

    [MaxLength(500, ErrorMessage = "Napomena može imati najviše 500 znakova.")]
    public string? Note { get; set; }

    [Required]
    public FulfillmentType Fulfillment { get; set; }

    [Required(ErrorMessage = "Unesite broj telefona.")]
    [RegularExpression(InputRules.Phone, ErrorMessage = "Unesite ispravan broj telefona.")]
    public string ContactPhone { get; set; } = string.Empty;

    // Obavezna samo kada robu nosi kurir
    [MaxLength(200, ErrorMessage = "Adresa može imati najviše 200 znakova.")]
    public string? DeliveryAddress { get; set; }
}

public class GuestOrderInputDto
{
    [Required]
    [MinLength(1)]
    public List<OrderItemInputDto> Items { get; set; } = new();

    [MaxLength(500, ErrorMessage = "Napomena može imati najviše 500 znakova.")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Unesite ime.")]
    [MinLength(2, ErrorMessage = "Unesite ime.")]
    [MaxLength(100, ErrorMessage = "Ime može imati najviše 100 znakova.")]
    public string GuestName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unesite broj telefona.")]
    [RegularExpression(InputRules.Phone, ErrorMessage = "Unesite ispravan broj telefona.")]
    public string GuestPhone { get; set; } = string.Empty;

    // Opciono: na ovu adresu se šalje link za praćenje porudžbine
    [EmailAddress(ErrorMessage = "Unesite ispravan e-mail.")]
    [MaxLength(200, ErrorMessage = "Unesite ispravan e-mail.")]
    public string? GuestEmail { get; set; }

    [Required]
    public FulfillmentType Fulfillment { get; set; }

    // Obavezna samo kada robu nosi kurir
    [MaxLength(200, ErrorMessage = "Adresa može imati najviše 200 znakova.")]
    public string? GuestAddress { get; set; }
}

// Kodovi za praćenje gostujućih porudžbina koje uređaj pamti
public class ClaimOrdersDto
{
    [MaxLength(20)]
    public List<string> TrackingCodes { get; set; } = new();
}

public class OrderItemInputDto
{
    [Required]
    public int ProductId { get; set; }

    // Pijačne količine: do 1000 (kg, komada...) po stavci; za komad i vezu samo celi brojevi (BuildOrders)
    [Required]
    [Range(0.01, 1000, ErrorMessage = "Količina mora biti između 0,01 i 1000.")]
    public decimal Quantity { get; set; }

    // Cena koju je kupac video u korpi: ako se u međuvremenu promenila, porudžbina se ne prima
    public decimal? ExpectedUnitPrice { get; set; }
}
