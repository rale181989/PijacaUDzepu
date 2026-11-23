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

// Obaveštenja: SignalR događaji, push kupcu i zvono prodavca.
public partial class OrderService
{
    // Javlja promenu statusa kupcu, prodavcu i dodeljenom kuriru (SignalR + push kupcu)
    private async Task NotifyStatusChanged(Order order, bool notifyCustomer = true, bool notifyVendor = true, bool notifyCourier = true)
    {
        // Poruka prodavca ide samo uz promenu uz koju je napisana, ne uz svaku sledeću
        var freshMessage = order.VendorMessageAt.HasValue && order.VendorMessageAt.Value >= order.UpdatedAt
            ? order.VendorMessage
            : null;
        var payload = new { orderId = order.Id, status = order.Status.ToString(), vendorName = order.Vendor.Name, message = freshMessage };

        if (notifyCustomer && order.TrackingCode != null)
            await _trackingHub.Clients.Group(TrackingHub.GroupName(order.TrackingCode)).SendAsync("OrderStatusChanged", payload);

        if (notifyCustomer && order.CustomerId.HasValue)
        {
            await _hubContext.Clients.Group($"user_{order.CustomerId.Value}").SendAsync("OrderStatusChanged", payload);

            var pickup = order.Fulfillment == FulfillmentType.Pickup;
            var text = order.Status switch
            {
                OrderStatus.Confirmed => pickup ? "Prodavac je potvrdio rezervaciju." : "Prodavac je potvrdio porudžbinu.",
                OrderStatus.Rejected => pickup ? "Prodavac je odbio rezervaciju." : "Prodavac je odbio porudžbinu.",
                OrderStatus.ReadyForPickup => pickup ? "Roba je spremna, možete doći po nju." : "Porudžbina je spremna i čeka kurira.",
                OrderStatus.InDelivery => "Kurir je preuzeo robu i krenuo ka vama.",
                OrderStatus.Completed => pickup ? "Rezervacija je preuzeta. Hvala!" : "Porudžbina je isporučena. Prijatno!",
                OrderStatus.NotDelivered => $"Porudžbina nije dostavljena. Razlog: {order.NotDeliveredReason}",
                _ => null
            };
            if (order.Status == OrderStatus.Rejected && freshMessage == UnansweredReason)
                text = UnansweredReason;
            else if (text != null && freshMessage != null)
                text += $" Poruka prodavca: {freshMessage}";
            if (text != null)
                await _pushService.SendToUsers(new[] { order.CustomerId.Value },
                    $"Porudžbina #{order.Id} – {order.Vendor.Name}", text, $"/order-detail/{order.Id}");
        }

        if (notifyVendor)
            await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("OrderStatusChanged", payload);

        // Dodeljeni kurir prati status (npr. "Spremno" znači da može po robu)
        if (notifyCourier && order.CourierId.HasValue)
        {
            await _hubContext.Clients.Group($"user_{order.CourierId.Value}").SendAsync("OrderStatusChanged", payload);
            if (order.Status is OrderStatus.ReadyForPickup or OrderStatus.Cancelled or OrderStatus.Rejected)
                await _pushService.SendToUsers(new[] { order.CourierId.Value },
                    $"Porudžbina #{order.Id}",
                    order.Status == OrderStatus.ReadyForPickup ? $"Roba je spremna kod: {order.Vendor.Name}." : "Porudžbina je otkazana.",
                    "/courier-orders");
        }
    }

    private async Task NotifyNewOrders(List<Order> orders)
    {
        foreach (var order in orders)
        {
            var customerName = order.Customer != null
                ? $"{order.Customer.FirstName} {order.Customer.LastName}"
                : order.GuestName ?? "Gost";

            await _hubContext.Clients.Group($"vendor_{order.VendorId}")
                .SendAsync("NewOrder", new
                {
                    orderId = order.Id,
                    customerName,
                    totalAmount = order.TotalAmount,
                    itemCount = order.Items.Count,
                    fulfillment = order.Fulfillment.ToString()
                });

            // Zvoni prodavcu dok porudžbinu ne prihvati ili odbije
            await _pushService.SendAlarm(await VendorUserIds(order.VendorId), VendorAlarmTag(order.Id),
                order.Fulfillment == FulfillmentType.Pickup ? $"Nova rezervacija #{order.Id}" : $"Nova porudžbina #{order.Id} (kurir)",
                $"{customerName} · {order.TotalAmount:0} din", "/vendor-orders");

            // Kuriri dobijaju porudžbinu tek kada je prodavac potvrdi (NotifyCouriersNewOrder)
        }
    }

    // Porudžbina više nije na čekanju: prestaje da zvoni prodavcu, i na drugim uređajima/nalozima iste tezge
    private async Task StopVendorAlarm(Order order)
    {
        await _hubContext.Clients.Group($"vendor_{order.VendorId}").SendAsync("PendingChanged", new { orderId = order.Id });
        await _pushService.CancelAlarm(await VendorUserIds(order.VendorId), VendorAlarmTag(order.Id));
    }

    private static string VendorAlarmTag(int orderId) => $"order_{orderId}";

    private static string CourierAlarmTag(int orderId) => $"courier_order_{orderId}";
}
