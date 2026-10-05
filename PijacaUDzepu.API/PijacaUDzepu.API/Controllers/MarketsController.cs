using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class MarketsController : BaseApiController
{
    private readonly IMarketService _marketService;

    public MarketsController(IMarketService marketService)
    {
        _marketService = marketService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _marketService.GetAll());
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAllIncludingInactive()
    {
        return Ok(await _marketService.GetAllIncludingInactive());
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(MarketInputDto dto)
    {
        return Ok(await _marketService.Create(dto));
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, MarketInputDto dto)
    {
        try
        {
            return Ok(await _marketService.Update(id, dto));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPut("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        try
        {
            await _marketService.ToggleActive(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
