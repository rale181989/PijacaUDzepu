using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Output;

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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users
            .Include(u => u.VendorUsers).ThenInclude(vu => vu.Vendor)
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
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
            });
        }

        return Ok(result);
    }

    [HttpPut("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound(new { message = "Korisnik nije pronađen." });

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return NoContent();
    }
}
