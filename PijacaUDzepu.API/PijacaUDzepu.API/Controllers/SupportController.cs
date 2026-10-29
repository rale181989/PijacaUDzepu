using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class SupportController : BaseApiController
{
    public const string RateLimitPolicy = "support";

    private readonly ISupportService _supportService;

    public SupportController(ISupportService supportService)
    {
        _supportService = supportService;
    }

    // Pitanje može da pošalje i gost; prijavljen korisnik se vezuje uz pitanje
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [HttpPost]
    public async Task<IActionResult> Create(SupportRequestInputDto dto)
    {
        // Botovi popune skriveno polje; odgovaramo kao da je uspelo da ih ne podstaknemo da pokušavaju dalje
        if (!string.IsNullOrEmpty(dto.Website)) return NoContent();

        int? userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        await _supportService.Create(dto, userId);
        return NoContent();
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? resolved)
    {
        return Ok(await _supportService.GetAll(resolved));
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpGet("open-count")]
    public async Task<IActionResult> GetOpenCount()
    {
        return Ok(new { count = await _supportService.GetOpenCount() });
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPut("{id}/resolved")]
    public async Task<IActionResult> SetResolved(int id, [FromQuery] bool value = true)
    {
        try
        {
            return Ok(await _supportService.SetResolved(id, value));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
