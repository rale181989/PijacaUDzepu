using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

// Omiljeni proizvodi i prodavci prijavljenog korisnika i obaveštenja "kad bude dostupno"
public class FavoritesController : BaseApiController
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _favoriteService.Get(User.GetUserId()));

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(FavoritesSyncDto dto) =>
        Ok(await _favoriteService.Sync(User.GetUserId(), dto.ProductIds, dto.VendorIds));

    [HttpPut("products/{id}")]
    public Task<IActionResult> AddProduct(int id) => Run(() => _favoriteService.SetProduct(User.GetUserId(), id, true));

    [HttpDelete("products/{id}")]
    public Task<IActionResult> RemoveProduct(int id) => Run(() => _favoriteService.SetProduct(User.GetUserId(), id, false));

    [HttpPut("vendors/{id}")]
    public Task<IActionResult> AddVendor(int id) => Run(() => _favoriteService.SetVendor(User.GetUserId(), id, true));

    [HttpDelete("vendors/{id}")]
    public Task<IActionResult> RemoveVendor(int id) => Run(() => _favoriteService.SetVendor(User.GetUserId(), id, false));

    [HttpPut("alerts/{productId}")]
    public Task<IActionResult> AddAlert(int productId) => Run(() => _favoriteService.SetStockAlert(User.GetUserId(), productId, true));

    [HttpDelete("alerts/{productId}")]
    public Task<IActionResult> RemoveAlert(int productId) => Run(() => _favoriteService.SetStockAlert(User.GetUserId(), productId, false));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
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
}
