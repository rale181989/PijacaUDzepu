using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;

namespace PijacaUDzepu.API.Hubs;

// Praćenje porudžbine bez naloga: gost se pretplati kodom iz linka za praćenje
// i dobija iste događaje kao prijavljeni kupac (promena statusa, dodeljen kurir).
[AllowAnonymous]
public class TrackingHub : Hub
{
    public static string GroupName(string code) => $"track_{code}";

    private readonly DataContext _context;

    public TrackingHub(DataContext context)
    {
        _context = context;
    }

    public async Task<bool> Subscribe(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64) return false;
        if (!await _context.Orders.AnyAsync(o => o.TrackingCode == code)) return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(code));
        return true;
    }
}
