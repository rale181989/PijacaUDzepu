using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Services;

public class MarketService : IMarketService
{
    private readonly DataContext _context;

    public MarketService(DataContext context)
    {
        _context = context;
    }

    public async Task<List<MarketDto>> GetAll()
    {
        return await _context.Markets
            .Include(m => m.Vendors)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => MapToDto(m))
            .ToListAsync();
    }

    public async Task<List<MarketDto>> GetAllIncludingInactive()
    {
        return await _context.Markets
            .Include(m => m.Vendors)
            .OrderBy(m => m.Name)
            .Select(m => MapToDto(m))
            .ToListAsync();
    }

    public async Task<MarketDto> GetById(int id)
    {
        var market = await _context.Markets
            .Include(m => m.Vendors)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new KeyNotFoundException(L.T("Pijaca nije pronađena"));
        return MapToDto(market);
    }

    // Centar pijace: obe koordinate ili nijedna, i u Srbiji (zamenjene širina i dužina daju tačku van nje)
    private static void ValidateCenter(MarketInputDto dto)
    {
        if (dto.Latitude is null != dto.Longitude is null)
            throw new InvalidOperationException(L.T("Upišite obe koordinate centra pijace (npr. 45.2529, 19.8371)."));
        if (dto.Latitude is double lat && dto.Longitude is double lng && !Features.InSerbia(lat, lng))
            throw new InvalidOperationException(L.T("Centar pijace mora biti u Srbiji (npr. 45.2529, 19.8371). Proverite da širina i dužina nisu zamenjene."));
    }

    public async Task<MarketDto> Create(MarketInputDto dto)
    {
        ValidateCenter(dto);
        var market = new Market
        {
            Name = dto.Name,
            Description = dto.Description,
            Address = dto.Address,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        _context.Markets.Add(market);
        await _context.SaveChangesAsync();
        return MapToDto(market);
    }

    public async Task<MarketDto> Update(int id, MarketInputDto dto)
    {
        ValidateCenter(dto);
        var market = await _context.Markets.FindAsync(id)
            ?? throw new KeyNotFoundException(L.T("Pijaca nije pronađena"));

        market.Name = dto.Name;
        market.Description = dto.Description;
        market.Address = dto.Address;
        market.Latitude = dto.Latitude;
        market.Longitude = dto.Longitude;

        await _context.SaveChangesAsync();
        return MapToDto(market);
    }

    public async Task ToggleActive(int id)
    {
        var market = await _context.Markets.FindAsync(id)
            ?? throw new KeyNotFoundException(L.T("Pijaca nije pronađena"));

        market.IsActive = !market.IsActive;
        await _context.SaveChangesAsync();
    }

    private static MarketDto MapToDto(Market m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        Address = m.Address,
        Latitude = m.Latitude,
        Longitude = m.Longitude,
        ImageUrl = m.ImageUrl,
        IsActive = m.IsActive,
        VendorCount = m.Vendors?.Count(v => v.IsActive) ?? 0
    };
}
