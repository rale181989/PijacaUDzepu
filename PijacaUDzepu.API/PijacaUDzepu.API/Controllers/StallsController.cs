using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StallsController : ControllerBase
{
    private readonly IStallService _stallService;

    public StallsController(IStallService stallService)
    {
        _stallService = stallService;
    }

    [HttpGet("by-market/{marketId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByMarket(int marketId)
    {
        return Ok(await _stallService.GetByMarket(marketId));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _stallService.GetAll());
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(StallInputDto dto)
    {
        return Ok(await _stallService.Create(dto));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(int id, StallInputDto dto)
    {
        return Ok(await _stallService.Update(id, dto));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _stallService.Delete(id);
        return NoContent();
    }
}
