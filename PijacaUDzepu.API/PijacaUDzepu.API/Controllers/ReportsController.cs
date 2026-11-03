using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Services.Interfaces;
using PijacaUDzepu.API.Helpers;

namespace PijacaUDzepu.API.Controllers;

// Izveštaji za SuperAdmin-a; period ?from=2026-11-01&to=2026-11-30.
// Bez "from" izveštaj počinje od prve porudžbine, bez "to" ide do danas.
[Authorize(Policy = "RequireSuperAdmin")]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t, error) = await GetRange(from, to);
        if (error != null) return BadRequest(new { message = error });
        return Ok(await _reportService.GetOrdersReport(f, t));
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t, error) = await GetRange(from, to);
        if (error != null) return BadRequest(new { message = error });
        return Ok(await _reportService.GetCustomersReport(f, t));
    }

    [HttpGet("couriers")]
    public async Task<IActionResult> GetCouriers([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t, error) = await GetRange(from, to);
        if (error != null) return BadRequest(new { message = error });
        return Ok(await _reportService.GetCouriersReport(f, t));
    }

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t, error) = await GetRange(from, to);
        if (error != null) return BadRequest(new { message = error });
        return Ok(await _reportService.GetVendorsReport(f, t));
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (f, t, error) = await GetRange(from, to);
        if (error != null) return BadRequest(new { message = error });
        return Ok(await _reportService.GetProductsReport(f, t));
    }

    [HttpGet("waiting")]
    public async Task<IActionResult> GetWaiting() => Ok(await _reportService.GetWaitingOrders());

    private async Task<(DateOnly from, DateOnly to, string? error)> GetRange(DateOnly? from, DateOnly? to)
    {
        var t = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (from > t)
            return (default, default, L.T("Početni datum mora biti pre krajnjeg."));

        // Bez početnog datuma: od prve porudžbine (ako je posle krajnjeg datuma, izveštaj je prazan)
        var f = from ?? await _reportService.GetFirstOrderDate();
        if (f > t) f = t;
        return (f, t, null);
    }
}
