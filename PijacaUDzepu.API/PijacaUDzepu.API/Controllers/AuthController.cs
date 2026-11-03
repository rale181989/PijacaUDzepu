using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Controllers;

public class AuthController : BaseApiController
{
    public const string RateLimitPolicy = "auth";

    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
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
    [EnableRateLimiting(RateLimitPolicy)]
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
    [HttpPost("create-courier")]
    public async Task<IActionResult> CreateCourier(CreateCourierDto dto)
    {
        try
        {
            var result = await _authService.CreateCourier(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
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
            return Ok(new { message = L.T("Pozivnica poslata.") });
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
            return Ok(new { message = L.T("Lozinka promenjena.") });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        await _authService.RequestPasswordReset(dto.Login);
        return Ok(new { message = L.T("Ako nalog postoji i ima e-mail adresu, poslali smo link za novu lozinku.") });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPassword(dto.Token, dto.NewPassword);
            return Ok(new { message = L.T("Lozinka je promenjena. Možete se prijaviti.") });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountDto dto)
    {
        try
        {
            await _authService.DeleteAccount(User.GetUserId(), dto.Password);
            return Ok(new { message = L.T("Nalog je obrisan.") });
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

    // Provera da API radi (Render health check); ne otkriva ništa o korisnicima
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
