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

// Entitet -> DTO: pun prikaz, prikaz za kupca/gosta (bez tačke tezge kod kurira, maskirani podaci) i za kurira pre prihvatanja.
public partial class OrderService
{
    // Stranicu za praćenje može da otvori svako ko ima link, pa adresa i telefon nisu prikazani u celosti
    // Kupac vidi tezgu na mapi samo kada sam dolazi po robu (rezervacija); kod kurira tačka mu ne treba
    public static OrderDto ForCustomer(OrderDto dto)
    {
        if (dto.Fulfillment == FulfillmentType.Courier)
        {
            dto.StallLatitude = null;
            dto.StallLongitude = null;
            dto.MarketLatitude = null;
            dto.MarketLongitude = null;
        }
        return dto;
    }

    private static OrderDto MapToTrackingDto(Order o)
    {
        var dto = ForCustomer(MapToDto(o));
        dto.DeliveryAddress = MaskAddress(o.DeliveryAddress);
        dto.ContactPhone = MaskPhone(o.ContactPhone);
        dto.ClosedByAdminName = null; // gost vidi samo da je zatvorio administrator, ne i ko
        return dto;
    }

    // Pre prihvatanja kurir vidi odakle i kuda se nosi, ali ne i ime i telefon kupca
    private static OrderDto MapToCourierPreview(Order o)
    {
        var dto = MapToDto(o);
        dto.CustomerName = string.Empty;
        dto.ContactPhone = null;
        dto.CustomerId = null;
        return dto;
    }

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
        StallLatitude = Features.StallLocation ? o.Vendor.StallLatitude : null,
        StallLongitude = Features.StallLocation ? o.Vendor.StallLongitude : null,
        MarketLatitude = Features.StallLocation ? o.Vendor.Market?.Latitude : null,
        MarketLongitude = Features.StallLocation ? o.Vendor.Market?.Longitude : null,
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
        CourierOfferedAt = o.CourierOfferedAt,
        AutoRejected = o.Status == OrderStatus.Rejected && o.RespondedAt == null && o.VendorMessage == UnansweredReason,
        RespondBy = o.Status == OrderStatus.Pending ? o.CreatedAt.AddMinutes(AutoRejectMinutes) : null,
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
