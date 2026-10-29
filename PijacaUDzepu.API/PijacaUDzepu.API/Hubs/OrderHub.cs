using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Hubs;

[Authorize]
public class OrderHub : Hub
{
    private readonly IVendorService _vendorService;

    public OrderHub(IVendorService vendorService)
    {
        _vendorService = vendorService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        var vendorId = await _vendorService.GetVendorIdForUser(userId);
        if (vendorId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"vendor_{vendorId.Value}");
        }

        // SuperAdmin dobija obaveštenje o novim pitanjima za podršku
        if (Context.User!.IsInRole("SuperAdmin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }

        // Svi kuriri dobijaju obaveštenje o novoj porudžbini
        if (Context.User!.IsInRole("Courier"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "couriers");
        }

        await base.OnConnectedAsync();
    }
}
