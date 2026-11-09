using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Helpers;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;
    private readonly IVendorService _vendorService;

    public OrdersController(IOrderService orderService, IVendorService vendorService)
    {
        _orderService = orderService;
        _vendorService = vendorService;
    }

    // Prodavac i kurir ne kupuju (ni kao gost dok su prijavljeni): poruči se sa kupčevog naloga ili bez naloga
    private bool CannotBuy => User.IsInRole("VendorAdmin") || User.IsInRole("Courier");
    private const string CannotBuyMessage = "Prodavci i kuriri ne mogu da poručuju robu. Odjavite se ili koristite kupčev nalog.";

    [HttpPost]
    public async Task<IActionResult> CreateOrder(OrderInputDto dto)
    {
        if (CannotBuy) return StatusCode(StatusCodes.Status403Forbidden, new { message = L.T(CannotBuyMessage) });
        try
        {
            var userId = User.GetUserId();
            var orders = await _orderService.CreateOrders(userId, dto);
            return Ok(orders);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("guest")]
    public async Task<IActionResult> CreateGuestOrder(GuestOrderInputDto dto)
    {
        if (CannotBuy) return StatusCode(StatusCodes.Status403Forbidden, new { message = L.T(CannotBuyMessage) });
        try
        {
            var orders = await _orderService.CreateGuestOrders(dto);
            return Ok(orders);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("delivery-options")]
    public IActionResult GetDeliveryOptions()
    {
        return Ok(new { courierFee = _orderService.CourierFee });
    }

    [AllowAnonymous]
    [HttpGet("track/{code}")]
    public async Task<IActionResult> Track(string code)
    {
        try
        {
            return Ok(await _orderService.GetTrackedOrders(code));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPut("track/{code}/{id}/cancel")]
    public async Task<IActionResult> CancelTracked(string code, int id)
    {
        try
        {
            return Ok(await _orderService.CancelTrackedOrder(code, id));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("track/{code}/{id}/rating")]
    public Task<IActionResult> RateTracked(string code, int id, OrderRatingInputDto dto) =>
        Rate(() => _orderService.RateOrder(id, null, code, dto));

    [HttpPost("{id}/rating")]
    public Task<IActionResult> Rate(int id, OrderRatingInputDto dto) =>
        Rate(() => _orderService.RateOrder(id, User.GetUserId(), null, dto));

    private async Task<IActionResult> Rate(Func<Task<Models.DTO.Output.OrderDto>> rate)
    {
        try
        {
            return Ok(await rate());
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

    // Broj porudžbina kupca u toku (crveni broj na ikonici "Moje porudžbine")
    [HttpGet("my-active-count")]
    public async Task<IActionResult> GetMyActiveCount()
    {
        return Ok(new { count = await _orderService.GetCustomerActiveCount(User.GetUserId()) });
    }

    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var userId = User.GetUserId();
        var result = await _orderService.GetCustomerOrders(userId, skip, take);
        return Ok(result);
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("vendor-orders")]
    public async Task<IActionResult> GetVendorOrders([FromQuery] bool finished = false, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        var result = await _orderService.GetVendorOrders(vendorId.Value, finished, skip, take);
        return Ok(result);
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("vendor-today")]
    public async Task<IActionResult> GetVendorToday()
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();
        return Ok(await _orderService.GetVendorToday(vendorId.Value));
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("vendor-pending-count")]
    public async Task<IActionResult> GetVendorPendingCount()
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        var count = await _orderService.GetVendorPendingCount(vendorId.Value);
        return Ok(new { count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var order = await _orderService.GetOrderById(id);
            var userId = User.GetUserId();

            if ((order.CustomerId == null || order.CustomerId != userId) && order.CourierId != userId)
            {
                var vendorId = await GetCurrentVendorId();
                if (vendorId == null || order.VendorId != vendorId.Value)
                    return Forbid();
            }

            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusDto dto)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        try
        {
            var order = await _orderService.UpdateOrderStatus(id, vendorId.Value, dto.Status, dto.Message);
            return Ok(order);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpGet("open")]
    public async Task<IActionResult> GetOpenOrders([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        return Ok(await _orderService.GetOpenOrders(skip, take));
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost("{id}/admin-complete")]
    public async Task<IActionResult> AdminComplete(int id)
    {
        try
        {
            return Ok(await _orderService.AdminCompleteOrder(id, User.GetUserId()));
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

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        try
        {
            var userId = User.GetUserId();
            var order = await _orderService.CancelOrder(id, userId);
            return Ok(order);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<int?> GetCurrentVendorId()
    {
        var userId = User.GetUserId();
        return await _vendorService.GetVendorIdForUser(userId);
    }
}
