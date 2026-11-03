using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Controllers;

[Authorize(Policy = "RequireSuperAdmin")]
public class UsersController : BaseApiController
{
    private readonly UserManager<User> _userManager;
    private readonly DataContext _context;

    public UsersController(UserManager<User> userManager, DataContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // Strana korisnika jedne uloge (Customer, VendorAdmin, Courier, SuperAdmin), uz pretragu
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role, [FromQuery] string? search,
        [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.UserName!, pattern) ||
                EF.Functions.ILike(u.FirstName, pattern) ||
                EF.Functions.ILike(u.LastName, pattern) ||
                EF.Functions.ILike((u.FirstName + " " + u.LastName), pattern) ||
                (u.Email != null && EF.Functions.ILike(u.Email, pattern)) ||
                (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, pattern)));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.UserName)
            .ThenBy(u => u.Id)
            .Skip(skip)
            .Take(take)
            .Include(u => u.VendorUsers).ThenInclude(vu => vu.Vendor)
            .ToListAsync();

        // Uloge za celu stranu jednim upitom (ranije: po jedan upit za svakog korisnika)
        var ids = users.Select(u => u.Id).ToList();
        var roles = (await _context.UserRoles
                .Where(ur => ids.Contains(ur.UserId))
                .Select(ur => new { ur.UserId, ur.Role.Name })
                .ToListAsync())
            .ToLookup(r => r.UserId, r => r.Name!);

        return Ok(new PagedResult<UserDto>
        {
            Items = users.Select(user => new UserDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Roles = roles[user.Id].ToList(),
                Vendors = user.VendorUsers.Select(vu => new VendorShortDto
                {
                    Id = vu.Vendor.Id,
                    Name = vu.Vendor.Name
                }).ToList()
            }).ToList(),
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        });
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromServices] Services.Interfaces.IAuthService authService)
    {
        try
        {
            var password = await authService.AdminResetPassword(id);
            return Ok(new { password });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound(new { message = L.T("Korisnik nije pronađen.") });

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return NoContent();
    }
}
