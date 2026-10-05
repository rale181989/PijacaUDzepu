using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class VendorService : IVendorService
{
    private readonly DataContext _context;
    private readonly IWebHostEnvironment _env;

    public VendorService(DataContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<List<VendorDto>> GetAllVendors(bool includeInactive = false)
    {
        var query = _context.Vendors.Include(v => v.Market).Include(v => v.Stall).AsQueryable();
        if (!includeInactive)
            query = query.Where(v => v.IsActive);

        return await query
            .OrderBy(v => v.Name)
            .Select(v => MapToDto(v))
            .ToListAsync();
    }

    public async Task<VendorDto> GetVendorById(int id)
    {
        var vendor = await _context.Vendors.Include(v => v.Market).Include(v => v.Stall)
            .FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");
        return MapToDto(vendor);
    }

    public async Task<VendorDto> CreateVendor(VendorInputDto dto)
    {
        var vendor = new Vendor
        {
            MarketId = dto.MarketId,
            StallId = dto.StallId,
            Name = dto.Name,
            Description = dto.Description,
            Address = dto.Address,
            Phone = dto.Phone
        };

        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync();

        await _context.Entry(vendor).Reference(v => v.Market).LoadAsync();
        if (vendor.StallId.HasValue)
            await _context.Entry(vendor).Reference(v => v.Stall).LoadAsync();
        return MapToDto(vendor);
    }

    public async Task<VendorDto> UpdateVendor(int id, VendorInputDto dto)
    {
        var vendor = await _context.Vendors.Include(v => v.Market).Include(v => v.Stall)
            .FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        vendor.MarketId = dto.MarketId;
        vendor.StallId = dto.StallId;
        vendor.Name = dto.Name;
        vendor.Description = dto.Description;
        vendor.Address = dto.Address;
        vendor.Phone = dto.Phone;
        vendor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _context.Entry(vendor).Reference(v => v.Market).LoadAsync();
        if (vendor.StallId.HasValue)
            await _context.Entry(vendor).Reference(v => v.Stall).LoadAsync();
        return MapToDto(vendor);
    }

    public async Task ToggleVendorActive(int id)
    {
        var vendor = await _context.Vendors.FindAsync(id)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        vendor.IsActive = !vendor.IsActive;
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<int?> GetVendorIdForUser(int userId)
    {
        var vendorUser = await _context.VendorUsers
            .FirstOrDefaultAsync(vu => vu.UserId == userId);
        return vendorUser?.VendorId;
    }

    public async Task<bool> UserBelongsToVendor(int userId, int vendorId)
    {
        return await _context.VendorUsers
            .AnyAsync(vu => vu.UserId == userId && vu.VendorId == vendorId);
    }

    public async Task<string> UpdateVendorImage(int vendorId, IFormFile file)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        var fileName = $"vendor_{vendorId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(_env.WebRootPath, "Resources", "Images", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        if (!string.IsNullOrEmpty(vendor.ImageUrl))
        {
            var oldPath = Path.Combine(_env.WebRootPath, vendor.ImageUrl.TrimStart('/'));
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        vendor.ImageUrl = $"/Resources/Images/{fileName}";
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return vendor.ImageUrl;
    }

    private static VendorDto MapToDto(Vendor v) => new()
    {
        Id = v.Id,
        MarketId = v.MarketId,
        MarketName = v.Market?.Name ?? "",
        StallId = v.StallId,
        StallLabel = v.Stall?.Label,
        Name = v.Name,
        Description = v.Description,
        Address = v.Address,
        Phone = v.Phone,
        ImageUrl = v.ImageUrl,
        IsActive = v.IsActive,
        CreatedAt = v.CreatedAt
    };
}
