using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly DataContext _context;
    private readonly ILogger<AuthService> _logger;

    private static readonly TimeSpan ResetLinkValidity = TimeSpan.FromHours(2);
    private static readonly OrderStatus[] ActiveStatuses =
        { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.ReadyForPickup, OrderStatus.InDelivery };

    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ITokenService tokenService,
        IEmailService emailService,
        DataContext context,
        ILogger<AuthService> logger)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _context = context;
    }

    public async Task<AuthResponseDto> Login(LoginDto dto)
    {
        var user = await _userManager.Users
            .Include(u => u.VendorUsers).ThenInclude(vu => vu.Vendor)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == dto.UserName.ToUpper());

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException(L.T("Pogrešno korisničko ime ili lozinka."));

        // Test nalog koji je seed zaključao u produkciji (vidi SeedData.LockDemoAccounts)
        if (user.LockoutEnd == DateTimeOffset.MaxValue)
            throw new UnauthorizedAccessException(L.T("Nalog je onemogućen. Obratite se administratoru."));

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            throw new UnauthorizedAccessException(L.T("Previše pogrešnih pokušaja. Nalog je privremeno zaključan, pokušajte ponovo za 15 minuta."));
        if (!result.Succeeded)
            throw new UnauthorizedAccessException(L.T("Pogrešno korisničko ime ili lozinka."));

        return new AuthResponseDto
        {
            Token = await _tokenService.CreateToken(user),
            User = await MapToUserDto(user)
        };
    }

    public async Task<AuthResponseDto> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new InvalidOperationException(L.T("Unesite broj telefona."));
        if (string.IsNullOrWhiteSpace(dto.Address))
            throw new InvalidOperationException(L.T("Unesite adresu."));

        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException(L.T("Korisničko ime je zauzeto."));

        var user = new User
        {
            UserName = dto.UserName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber.Trim(),
            Address = dto.Address.Trim(),
            Floor = Clean(dto.Floor),
            Apartment = Clean(dto.Apartment)
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "Customer");

        return new AuthResponseDto
        {
            Token = await _tokenService.CreateToken(user),
            User = await MapToUserDto(user)
        };
    }

    public async Task<UserDto> CreateVendorAdmin(CreateVendorAdminDto dto)
    {
        var vendor = await _context.Vendors.FindAsync(dto.VendorId)
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException(L.T("Korisničko ime je zauzeto."));

        var user = new User
        {
            UserName = dto.UserName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "VendorAdmin");

        _context.VendorUsers.Add(new VendorUser { UserId = user.Id, VendorId = dto.VendorId });
        await _context.SaveChangesAsync();

        return await MapToUserDto(user);
    }

    public async Task<UserDto> CreateCourier(CreateCourierDto dto)
    {
        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException(L.T("Korisničko ime je zauzeto."));

        var user = new User
        {
            UserName = dto.UserName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            CourierProfile = new CourierProfile()
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "Courier");

        return await MapToUserDto(user);
    }

    public async Task InviteVendor(InviteVendorDto dto)
    {
        var vendor = await _context.Vendors.FindAsync(dto.VendorId)
            ?? throw new KeyNotFoundException(L.T("Prodavac nije pronađen."));

        var existing = await _context.VendorInvitations
            .AnyAsync(vi => vi.VendorId == dto.VendorId && !vi.IsUsed && vi.ExpiresAt > DateTime.UtcNow);
        if (existing)
            throw new InvalidOperationException(L.T("Aktivna pozivnica za ovog prodavca već postoji."));

        var invitation = new VendorInvitation
        {
            VendorId = dto.VendorId,
            Email = dto.Email.Trim().ToLower(),
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.VendorInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        await _emailService.SendVendorInvitation(dto.Email, vendor.Name, invitation.Token);
    }

    public async Task<AuthResponseDto> AcceptInvitation(AcceptInvitationDto dto)
    {
        var invitation = await _context.VendorInvitations
            .Include(vi => vi.Vendor)
            .FirstOrDefaultAsync(vi => vi.Token == dto.Token && !vi.IsUsed && vi.ExpiresAt > DateTime.UtcNow)
            ?? throw new InvalidOperationException(L.T("Pozivnica nije validna ili je istekla."));

        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException(L.T("Korisničko ime je zauzeto."));

        var user = new User
        {
            UserName = dto.UserName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = invitation.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, "VendorAdmin");

        _context.VendorUsers.Add(new VendorUser { UserId = user.Id, VendorId = invitation.VendorId });
        invitation.IsUsed = true;
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = await _tokenService.CreateToken(user),
            User = await MapToUserDto(user)
        };
    }

    public async Task<UserDto> UpdateProfile(int userId, UpdateProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new InvalidOperationException(L.T("Unesite broj telefona."));

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException(L.T("Korisnik nije pronađen."));

        user.PhoneNumber = dto.PhoneNumber.Trim();
        user.Address = Clean(dto.Address);
        user.Floor = Clean(dto.Floor);
        user.Apartment = Clean(dto.Apartment);
        await _userManager.UpdateAsync(user);

        return await MapToUserDto(user);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException(L.T("Korisnik nije pronađen."));

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                result.Errors.Any(e => e.Code == "PasswordMismatch")
                    ? L.T("Pogrešna trenutna lozinka.")
                    : string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // Odgovor je isti i kada nalog ne postoji ili nema e-mail, da se ne bi moglo proveravati
    // koja korisnička imena postoje. E-mail se šalje u pozadini (i da vreme odgovora ne otkriva nalog).
    public async Task RequestPasswordReset(string login)
    {
        var normalized = login.Trim().ToUpperInvariant();
        var user = await _userManager.Users.FirstOrDefaultAsync(u =>
            u.NormalizedUserName == normalized || u.NormalizedEmail == normalized);
        if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _context.PasswordResets.Add(new PasswordReset
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.Add(ResetLinkValidity)
        });
        await _context.SaveChangesAsync();

        var emailService = _emailService;
        var logger = _logger;
        var (email, name) = (user.Email, user.FirstName);
        _ = Task.Run(async () =>
        {
            try { await emailService.SendPasswordReset(email, name, token); }
            catch (Exception ex) { logger.LogError(ex, "Slanje linka za novu lozinku na {Email} nije uspelo.", email); }
        });
    }

    public async Task ResetPassword(string token, string newPassword)
    {
        var hash = HashToken(token.Trim());
        var reset = await _context.PasswordResets.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (reset == null || reset.UsedAt != null || reset.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException(L.T("Link je istekao ili je već iskorišćen. Zatražite novi."));

        var user = reset.User;
        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, identityToken, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        // Nova lozinka otključava nalog zaključan zbog pogrešnih pokušaja
        await _userManager.ResetAccessFailedCountAsync(user);
        if (user.LockoutEnd != DateTimeOffset.MaxValue)
            await _userManager.SetLockoutEndDateAsync(user, null);

        reset.UsedAt = DateTime.UtcNow;
        // Ostali neiskorišćeni linkovi istog korisnika više ne važe
        await _context.PasswordResets
            .Where(r => r.UserId == user.Id && r.UsedAt == null && r.Id != reset.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.UsedAt, DateTime.UtcNow));
        await _context.SaveChangesAsync();
    }

    // Brisanje naloga (zahtev Google Play-a i zakona o zaštiti podataka). Porudžbine ostaju prodavcima
    // za evidenciju, ali bez ličnih podataka kupca; dok traje porudžbina ili dostava, nalog se ne briše.
    public async Task DeleteAccount(int userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException(L.T("Korisnik nije pronađen."));
        if (!await _userManager.CheckPasswordAsync(user, password))
            throw new InvalidOperationException(L.T("Pogrešna lozinka."));

        if (await _userManager.IsInRoleAsync(user, "SuperAdmin")
            && (await _userManager.GetUsersInRoleAsync("SuperAdmin")).Count <= 1)
            throw new InvalidOperationException(L.T("Ovo je jedini administratorski nalog i ne može se obrisati."));

        if (await _context.Orders.AnyAsync(o => o.CustomerId == userId && ActiveStatuses.Contains(o.Status)))
            throw new InvalidOperationException(L.T("Imate porudžbine u toku. Nalog možete obrisati kada budu završene."));
        if (await _context.Orders.AnyAsync(o => o.CourierId == userId && ActiveStatuses.Contains(o.Status)))
            throw new InvalidOperationException(L.T("Imate dostave u toku. Nalog možete obrisati kada ih završite."));

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.Orders
            .Where(o => o.CustomerId == userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(o => o.CustomerId, (int?)null)
                .SetProperty(o => o.GuestName, "Obrisan nalog")
                .SetProperty(o => o.GuestEmail, (string?)null)
                .SetProperty(o => o.ContactPhone, (string?)null)
                .SetProperty(o => o.DeliveryAddress, (string?)null)
                .SetProperty(o => o.Note, (string?)null));

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await transaction.CommitAsync();
        _logger.LogInformation("Nalog #{UserId} je obrisan na zahtev korisnika.", userId);
    }

    // Admin postavlja privremenu lozinku (za kurire i prodavce bez e-maila) i otključava nalog
    public async Task<string> AdminResetPassword(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException(L.T("Korisnik nije pronađen."));

        const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
        var password = new string(Enumerable.Range(0, 10).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);
        return password;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<UserDto> GetCurrentUser(int userId)
    {
        var user = await _userManager.Users
            .Include(u => u.VendorUsers).ThenInclude(vu => vu.Vendor)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException(L.T("Korisnik nije pronađen."));

        return await MapToUserDto(user);
    }

    private async Task<UserDto> MapToUserDto(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        if (!user.VendorUsers.Any())
        {
            await _context.Entry(user).Collection(u => u.VendorUsers).LoadAsync();
        }

        foreach (var vu in user.VendorUsers.Where(v => v.Vendor == null))
        {
            await _context.Entry(vu).Reference(v => v.Vendor).LoadAsync();
        }

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Floor = user.Floor,
            Apartment = user.Apartment,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            Vendors = user.VendorUsers.Select(vu => new VendorShortDto
            {
                Id = vu.Vendor.Id,
                Name = vu.Vendor.Name
            }).ToList()
        };
    }
}
