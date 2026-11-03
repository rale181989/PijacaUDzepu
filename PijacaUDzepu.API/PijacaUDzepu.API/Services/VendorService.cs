using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Helpers;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class VendorService : IVendorService
{
    private readonly DataContext _context;
    private readonly IImageStorage _images;

    public VendorService(DataContext context, IImageStorage images)
    {
        _context = context;
        _images = images;
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
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));
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
            Phones = NormalizePhones(dto.Phones),
            AcceptsReservations = dto.AcceptsReservations,
            OffersDelivery = dto.OffersDelivery,
            MinOrderAmount = dto.OffersDelivery ? dto.MinOrderAmount : null,
            DeliveryRadiusKm = dto.OffersDelivery ? dto.DeliveryRadiusKm : null,
            DeliverySchedule = dto.OffersDelivery ? dto.DeliverySchedule : null
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
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        vendor.MarketId = dto.MarketId;
        vendor.StallId = dto.StallId;
        vendor.Name = dto.Name;
        vendor.Description = dto.Description;
        vendor.Address = dto.Address;
        vendor.Phones = NormalizePhones(dto.Phones);
        vendor.AcceptsReservations = dto.AcceptsReservations;
        vendor.OffersDelivery = dto.OffersDelivery;
        vendor.MinOrderAmount = dto.OffersDelivery ? dto.MinOrderAmount : null;
        vendor.DeliveryRadiusKm = dto.OffersDelivery ? dto.DeliveryRadiusKm : null;
        vendor.DeliverySchedule = dto.OffersDelivery ? dto.DeliverySchedule : null;
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
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

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

    public async Task<VendorDto> UpdateAvailability(int vendorId, VendorAvailabilityDto dto)
    {
        var vendor = await _context.Vendors.Include(v => v.Market).Include(v => v.Stall).FirstOrDefaultAsync(v => v.Id == vendorId)
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        var invalid = dto.OpeningHours.FirstOrDefault(h => !VendorAvailability.IsValid(h));
        if (invalid != null)
            throw new InvalidOperationException(L.T("Radno vreme nije ispravno: početak mora biti pre kraja (npr. 07:00 – 14:00)."));

        vendor.OrdersPaused = dto.OrdersPaused;
        // Jedan unos po danu; prošli neradni dani se ne čuvaju
        vendor.OpeningHours = dto.OpeningHours.GroupBy(h => h.Day).Select(g => g.First()).OrderBy(h => h.Day).ToList();
        vendor.ClosedDates = dto.ClosedDates.Where(d => d >= LocalTime.Today).Distinct().OrderBy(d => d).Take(60).ToList();
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToDto(vendor);
    }

    public async Task<string> UpdateVendorImage(int vendorId, IFormFile file)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId)
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        var jpeg = await ImageResizer.ToJpeg(file);
        var newUrl = await _images.Save(jpeg, $"vendors/vendor_{vendorId}_{Guid.NewGuid()}.jpg", ImageResizer.ContentType);

        // Stara slika se briše tek kad baza pokazuje na novu; ako čuvanje padne, briše se nova
        var oldUrl = vendor.ImageUrl;
        vendor.ImageUrl = newUrl;
        vendor.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            await _images.Delete(newUrl);
            throw;
        }

        await _images.Delete(oldUrl);
        return newUrl;
    }

    private static List<string> NormalizePhones(List<string>? phones) =>
        (phones ?? new())
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct()
            .ToList();

    private static VendorDto MapToDto(Vendor v)
    {
        var status = VendorAvailability.Evaluate(v);
        return new VendorDto
        {
        Id = v.Id,
        MarketId = v.MarketId,
        MarketName = v.Market?.Name ?? "",
        StallId = v.StallId,
        StallLabel = v.Stall?.Label,
        Name = v.Name,
        Description = v.Description,
        Address = v.Address,
        Phones = v.Phones,
        ImageUrl = v.ImageUrl,
        IsActive = v.IsActive,
        AcceptsReservations = v.AcceptsReservations,
        OffersDelivery = v.OffersDelivery,
        MinOrderAmount = v.MinOrderAmount,
        DeliveryRadiusKm = v.DeliveryRadiusKm,
        DeliverySchedule = v.DeliverySchedule,
        OrdersPaused = v.OrdersPaused,
        OpeningHours = v.OpeningHours ?? new(),
        ClosedDates = v.ClosedDates ?? new(),
        IsOpen = status.IsOpen,
        ClosedReason = status.ClosedReason,
        OpensAt = status.OpensAt,
        ClosesAt = status.ClosesAt,
        CreatedAt = v.CreatedAt
        };
    }
}
