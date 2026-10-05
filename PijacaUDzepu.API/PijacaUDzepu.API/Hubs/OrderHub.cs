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

        await base.OnConnectedAsync();
    }
}
