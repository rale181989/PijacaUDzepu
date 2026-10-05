using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class StallService : IStallService
{
    private readonly DataContext _context;

    public StallService(DataContext context)
    {
        _context = context;
    }

    public async Task<List<StallDto>> GetByMarket(int marketId)
    {
        return await _context.Stalls
            .Include(s => s.Market)
            .Where(s => s.MarketId == marketId && s.IsActive)
            .OrderBy(s => s.Label)
            .Select(s => MapToDto(s))
            .ToListAsync();
    }

    public async Task<List<StallDto>> GetAll()
    {
        return await _context.Stalls
            .Include(s => s.Market)
            .OrderBy(s => s.Market.Name).ThenBy(s => s.Label)
            .Select(s => MapToDto(s))
            .ToListAsync();
    }

    public async Task<StallDto> Create(StallInputDto dto)
    {
        var stall = new Stall
        {
            MarketId = dto.MarketId,
            Label = dto.Label
        };

        _context.Stalls.Add(stall);
        await _context.SaveChangesAsync();

        await _context.Entry(stall).Reference(s => s.Market).LoadAsync();
        return MapToDto(stall);
    }

    public async Task<StallDto> Update(int id, StallInputDto dto)
    {
        var stall = await _context.Stalls.Include(s => s.Market)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Tezga nije pronađena");

        stall.MarketId = dto.MarketId;
        stall.Label = dto.Label;

        await _context.SaveChangesAsync();

        await _context.Entry(stall).Reference(s => s.Market).LoadAsync();
        return MapToDto(stall);
    }

    public async Task Delete(int id)
    {
        var stall = await _context.Stalls.FindAsync(id)
            ?? throw new KeyNotFoundException("Tezga nije pronađena");

        stall.IsActive = false;
        await _context.SaveChangesAsync();
    }

    private static StallDto MapToDto(Stall s) => new()
    {
        Id = s.Id,
        MarketId = s.MarketId,
        MarketName = s.Market?.Name ?? "",
        Label = s.Label,
        IsActive = s.IsActive
    };
}
