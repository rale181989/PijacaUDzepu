using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Services;

// Omiljeni proizvodi/prodavci i obaveštenja "kad bude dostupno" (samo za prijavljene korisnike;
// gosti čuvaju omiljene na uređaju, a posle prijave se prenose preko Sync)
public class FavoriteService : IFavoriteService
{
    private readonly DataContext _context;
    private readonly IPushService _pushService;
    private readonly IHubContext<OrderHub> _hubContext;

    public FavoriteService(DataContext context, IPushService pushService, IHubContext<OrderHub> hubContext)
    {
        _context = context;
        _pushService = pushService;
        _hubContext = hubContext;
    }

    public async Task<FavoritesDto> Get(int userId)
    {
        var favorites = await _context.Favorites.Where(f => f.UserId == userId).ToListAsync();
        return new FavoritesDto
        {
            ProductIds = favorites.Where(f => f.ProductId != null).Select(f => f.ProductId!.Value).ToList(),
            VendorIds = favorites.Where(f => f.VendorId != null).Select(f => f.VendorId!.Value).ToList(),
            AlertProductIds = await _context.StockAlerts.Where(a => a.UserId == userId).Select(a => a.ProductId).ToListAsync()
        };
    }

    public async Task SetProduct(int userId, int productId, bool favorite)
    {
        var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);
        if (favorite && existing == null)
        {
            if (!await _context.Products.AnyAsync(p => p.Id == productId))
                throw new KeyNotFoundException(L.T("Proizvod nije pronađen."));
            _context.Favorites.Add(new Favorite { UserId = userId, ProductId = productId });
        }
        else if (!favorite && existing != null)
        {
            _context.Favorites.Remove(existing);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SetVendor(int userId, int vendorId, bool favorite)
    {
        var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.VendorId == vendorId);
        if (favorite && existing == null)
        {
            if (!await _context.Vendors.AnyAsync(v => v.Id == vendorId))
                throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));
            _context.Favorites.Add(new Favorite { UserId = userId, VendorId = vendorId });
        }
        else if (!favorite && existing != null)
        {
            _context.Favorites.Remove(existing);
        }
        await _context.SaveChangesAsync();
    }

    // Omiljeni sačuvani na uređaju pre prijave dodaju se nalogu
    public async Task<FavoritesDto> Sync(int userId, List<int> productIds, List<int> vendorIds)
    {
        var current = await Get(userId);
        var validProducts = await _context.Products
            .Where(p => productIds.Contains(p.Id) && !current.ProductIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        var validVendors = await _context.Vendors
            .Where(v => vendorIds.Contains(v.Id) && !current.VendorIds.Contains(v.Id)).Select(v => v.Id).ToListAsync();

        _context.Favorites.AddRange(validProducts.Select(id => new Favorite { UserId = userId, ProductId = id }));
        _context.Favorites.AddRange(validVendors.Select(id => new Favorite { UserId = userId, VendorId = id }));
        await _context.SaveChangesAsync();
        return await Get(userId);
    }

    public async Task SetStockAlert(int userId, int productId, bool enabled)
    {
        var existing = await _context.StockAlerts.FirstOrDefaultAsync(a => a.UserId == userId && a.ProductId == productId);
        if (enabled && existing == null)
        {
            var product = await _context.Products.FindAsync(productId)
                ?? throw new KeyNotFoundException(L.T("Proizvod nije pronađen."));
            if (product.IsAvailable)
                throw new InvalidOperationException(L.T("Proizvod je već dostupan."));
            _context.StockAlerts.Add(new StockAlert { UserId = userId, ProductId = productId });
        }
        else if (!enabled && existing != null)
        {
            _context.StockAlerts.Remove(existing);
        }
        await _context.SaveChangesAsync();
    }

    // Poziva ProductService kada proizvod ponovo postane dostupan
    public async Task NotifyBackInStock(Product product, string vendorName)
    {
        var alerts = await _context.StockAlerts.Where(a => a.ProductId == product.Id).ToListAsync();
        if (alerts.Count == 0) return;

        var userIds = alerts.Select(a => a.UserId).Distinct().ToList();
        var payload = new { productId = product.Id, productName = product.Name, vendorName };
        await _hubContext.Clients.Groups(userIds.Select(id => $"user_{id}").ToList()).SendAsync("StockAvailable", payload);
        await _pushService.SendToUsers(userIds, "Ponovo dostupno", $"{product.Name} – {vendorName}", "/home");

        _context.StockAlerts.RemoveRange(alerts);
        await _context.SaveChangesAsync();
    }
}
