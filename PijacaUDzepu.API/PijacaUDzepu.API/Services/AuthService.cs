using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly DataContext _context;

    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ITokenService tokenService,
        IEmailService emailService,
        DataContext context)
    {
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
            throw new UnauthorizedAccessException("Pogrešno korisničko ime ili lozinka.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded)
            throw new UnauthorizedAccessException("Pogrešno korisničko ime ili lozinka.");

        return new AuthResponseDto
        {
            Token = await _tokenService.CreateToken(user),
            User = await MapToUserDto(user)
        };
    }

    public async Task<AuthResponseDto> Register(RegisterDto dto)
    {
        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException("Korisničko ime je zauzeto.");

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
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException("Korisničko ime je zauzeto.");

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

    public async Task InviteVendor(InviteVendorDto dto)
    {
        var vendor = await _context.Vendors.FindAsync(dto.VendorId)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        var existing = await _context.VendorInvitations
            .AnyAsync(vi => vi.VendorId == dto.VendorId && !vi.IsUsed && vi.ExpiresAt > DateTime.UtcNow);
        if (existing)
            throw new InvalidOperationException("Aktivna pozivnica za ovog prodavca već postoji.");

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
            ?? throw new InvalidOperationException("Pozivnica nije validna ili je istekla.");

        if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == dto.UserName.ToUpper()))
            throw new InvalidOperationException("Korisničko ime je zauzeto.");

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

    public async Task ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException("Korisnik nije pronađen.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                result.Errors.Any(e => e.Code == "PasswordMismatch")
                    ? "Pogrešna trenutna lozinka."
                    : string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public async Task<UserDto> GetCurrentUser(int userId)
    {
        var user = await _userManager.Users
            .Include(u => u.VendorUsers).ThenInclude(vu => vu.Vendor)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Korisnik nije pronađen.");

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
