using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try
        {
            var result = await _authService.Login(dto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        try
        {
            var result = await _authService.Register(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost("create-vendor-admin")]
    public async Task<IActionResult> CreateVendorAdmin(CreateVendorAdminDto dto)
    {
        try
        {
            var result = await _authService.CreateVendorAdmin(dto);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost("invite-vendor")]
    public async Task<IActionResult> InviteVendor(InviteVendorDto dto)
    {
        try
        {
            await _authService.InviteVendor(dto);
            return Ok(new { message = "Pozivnica poslata." });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("accept-invitation")]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationDto dto)
    {
        try
        {
            var result = await _authService.AcceptInvitation(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        try
        {
            var userId = User.GetUserId();
            await _authService.ChangePassword(userId, dto.CurrentPassword, dto.NewPassword);
            return Ok(new { message = "Lozinka promenjena." });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.GetUserId();
        var result = await _authService.GetCurrentUser(userId);
        return Ok(result);
    }
}
