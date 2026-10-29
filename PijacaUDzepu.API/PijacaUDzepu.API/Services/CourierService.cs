using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class CourierService : ICourierService
{
    private readonly DataContext _context;

    public CourierService(DataContext context)
    {
        _context = context;
    }

    public async Task<CourierProfileDto> GetProfile(int userId)
    {
        var user = await LoadUser(userId);
        return MapToDto(user);
    }

    public async Task<CourierProfileDto> UpdateProfile(int userId, CourierProfileInputDto dto)
    {
        var user = await LoadUser(userId);

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.PhoneNumber = dto.PhoneNumber.Trim();

        user.CourierProfile ??= new CourierProfile { UserId = user.Id };
        user.CourierProfile.Vehicle = dto.Vehicle;
        user.CourierProfile.LicensePlate = string.IsNullOrWhiteSpace(dto.LicensePlate)
            ? null
            : dto.LicensePlate.Trim().ToUpperInvariant();
        user.CourierProfile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(user);
    }

    public async Task<CourierProfileDto> SetShift(int userId, bool isOnShift)
    {
        var user = await LoadUser(userId);
        user.CourierProfile ??= new CourierProfile { UserId = user.Id };
        user.CourierProfile.IsOnShift = isOnShift;
        user.CourierProfile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToDto(user);
    }

    private async Task<User> LoadUser(int userId) =>
        await _context.Users
            .Include(u => u.CourierProfile)
            .FirstOrDefaultAsync(u => u.Id == userId)
        ?? throw new KeyNotFoundException("Kurir nije pronađen.");

    private static CourierProfileDto MapToDto(User u) => new()
    {
        UserId = u.Id,
        UserName = u.UserName ?? "",
        FirstName = u.FirstName,
        LastName = u.LastName,
        PhoneNumber = u.PhoneNumber,
        Vehicle = u.CourierProfile?.Vehicle,
        LicensePlate = u.CourierProfile?.LicensePlate,
        IsOnShift = u.CourierProfile?.IsOnShift ?? false
    };
}
