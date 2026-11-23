using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Hubs;
using Microsoft.AspNetCore.SignalR;
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

    private readonly IHubContext<OrderHub> _hub;
    private readonly IHubContext<TrackingHub> _trackingHub;

    public VendorService(DataContext context, IImageStorage images, IHubContext<OrderHub> hub, IHubContext<TrackingHub> trackingHub)
    {
        _context = context;
        _images = images;
        _hub = hub;
        _trackingHub = trackingHub;
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

        // Druga pijaca ili tezga: stara tačka na mapi više ne važi (prodavac je ponovo postavlja na novoj tezgi)
        if (vendor.MarketId != dto.MarketId || vendor.StallId != dto.StallId)
        {
            vendor.StallLatitude = null;
            vendor.StallLongitude = null;
            vendor.StallLocationAt = null;
        }
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

        var badValue = dto.OpeningHours.FirstOrDefault(h => h.Day is < 1 or > 7
            || !VendorAvailability.TryParseTime(h.From, out _) || !VendorAvailability.TryParseTime(h.To, out _));
        if (badValue != null)
            throw new InvalidOperationException(L.T("Radno vreme nije ispravno: dan ili vreme ne postoji (vreme kao 07:00)."));
        var invalid = dto.OpeningHours.FirstOrDefault(h => !VendorAvailability.IsValid(h));
        if (invalid != null)
            throw new InvalidOperationException(L.T("Radno vreme nije ispravno: početak mora biti pre kraja (npr. 07:00 – 14:00)."));

        vendor.OrdersPaused = dto.OrdersPaused;
        // Jedan unos po danu; prošli neradni dani se ne čuvaju
        vendor.OpeningHours = dto.OpeningHours.GroupBy(h => h.Day).Select(g => g.First()).OrderBy(h => h.Day).ToList();
        vendor.ClosedDates = dto.ClosedDates.Where(d => d >= LocalTime.Today).Distinct().OrderBy(d => d).Take(60).ToList();
        if (string.IsNullOrWhiteSpace(dto.EarlyCloseToday))
        {
            vendor.EarlyCloseDate = null;
            vendor.EarlyCloseTime = null;
        }
        else
        {
            if (!VendorAvailability.TryParseTime(dto.EarlyCloseToday.Trim(), out var at))
                throw new InvalidOperationException(L.T("Vreme zatvaranja nije ispravno (npr. 14:00)."));
            vendor.EarlyCloseDate = LocalTime.Today;
            vendor.EarlyCloseTime = at.ToString("HH:mm");
        }
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToDto(vendor);
    }

    // Prodavac je na tezgi pritisnuo "Ovde sam" (ili pomerio tačku na mapi). Tačka mora biti blizu pijace,
    // da se ne sačuva slučajno kućna adresa ili GPS koji je "odleteo"; null briše tačku.
    public const double MaxStallDistanceMeters = 1500;

    public async Task<VendorDto> UpdateStallLocation(int vendorId, StallLocationDto? dto)
    {
        if (!Features.StallLocation)
            throw new InvalidOperationException(L.T("Lokacija tezge je isključena."));
        var vendor = await _context.Vendors.Include(v => v.Market).Include(v => v.Stall).FirstOrDefaultAsync(v => v.Id == vendorId)
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        if (dto != null)
        {
            if (dto.Latitude is not double lat || dto.Longitude is not double lng)
                throw new InvalidOperationException(L.T("Lokacija nije ispravna."));
            // Bez centra pijace ne može da se proveri da je tačka na pijaci (npr. kućna adresa)
            if (vendor.Market.Latitude is not double mLat || vendor.Market.Longitude is not double mLng)
                throw new InvalidOperationException(L.T("Pijaca {0} još nema centar na mapi. Javite se administratoru.", vendor.Market.Name));
            if (GeoDistance.Meters(mLat, mLng, lat, lng) > MaxStallDistanceMeters)
                throw new InvalidOperationException(L.T("Tačka je predaleko od pijace {0}. Postavite je kada ste na tezgi ili je pomerite na mapi.", vendor.Market.Name));
        }

        vendor.StallLatitude = dto?.Latitude;
        vendor.StallLongitude = dto?.Longitude;
        vendor.StallLocationAt = dto == null ? null : DateTime.UtcNow;
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await NotifyStallMoved(vendor.Id);
        return MapToDto(vendor);
    }

    // Kurir i kupac sa otvorenom porudžbinom kod ove tezge odmah vide novu tačku (StallLocationChanged → ponovno učitavanje)
    private async Task NotifyStallMoved(int vendorId)
    {
        var open = await _context.Orders
            .Where(o => o.VendorId == vendorId && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed
                || o.Status == OrderStatus.ReadyForPickup || o.Status == OrderStatus.InDelivery))
            .Select(o => new { o.CustomerId, o.CourierId, o.TrackingCode })
            .ToListAsync();
        var payload = new { vendorId };
        var users = open.SelectMany(o => new[] { o.CustomerId, o.CourierId }).Where(id => id != null).Distinct()
            .Select(id => $"user_{id}").ToList();
        if (users.Count > 0)
            await _hub.Clients.Groups(users).SendAsync("StallLocationChanged", payload);
        foreach (var code in open.Select(o => o.TrackingCode).Where(c => c != null).Distinct())
            await _trackingHub.Clients.Group(TrackingHub.GroupName(code!)).SendAsync("StallLocationChanged", payload);
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
        EarlyCloseToday = v.EarlyCloseDate == LocalTime.Today ? v.EarlyCloseTime : null,
        OpeningHours = v.OpeningHours ?? new(),
        ClosedDates = v.ClosedDates ?? new(),
        IsOpen = status.IsOpen,
        ClosedReason = status.ClosedReason,
        OpensAt = status.OpensAt,
        ClosesAt = status.ClosesAt,
        StallLatitude = Features.StallLocation ? v.StallLatitude : null,
        StallLongitude = Features.StallLocation ? v.StallLongitude : null,
        StallLocationAt = Features.StallLocation ? v.StallLocationAt : null,
        MarketLatitude = v.Market?.Latitude,
        MarketLongitude = v.Market?.Longitude,
        CreatedAt = v.CreatedAt
        };
    }
}
