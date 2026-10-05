using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
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

    [HttpPost]
    public async Task<IActionResult> CreateOrder(OrderInputDto dto)
    {
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

    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var userId = User.GetUserId();
        var result = await _orderService.GetCustomerOrders(userId, skip, take);
        return Ok(result);
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("vendor-orders")]
    public async Task<IActionResult> GetVendorOrders([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        var result = await _orderService.GetVendorOrders(vendorId.Value, skip, take);
        return Ok(result);
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

            if (order.CustomerId == null || order.CustomerId != userId)
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
            var order = await _orderService.UpdateOrderStatus(id, vendorId.Value, dto.Status);
            return Ok(order);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
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
