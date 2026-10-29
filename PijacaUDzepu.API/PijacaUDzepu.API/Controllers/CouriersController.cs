using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

[Authorize(Policy = "RequireCourier")]
public class CouriersController : BaseApiController
{
    private readonly ICourierService _courierService;
    private readonly IOrderService _orderService;

    public CouriersController(ICourierService courierService, IOrderService orderService)
    {
        _courierService = courierService;
        _orderService = orderService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await _courierService.GetProfile(User.GetUserId());
        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(CourierProfileInputDto dto)
    {
        var profile = await _courierService.UpdateProfile(User.GetUserId(), dto);
        return Ok(profile);
    }

    [HttpPut("me/shift")]
    public async Task<IActionResult> SetShift(CourierShiftDto dto)
    {
        var profile = await _courierService.SetShift(User.GetUserId(), dto.IsOnShift);
        return Ok(profile);
    }

    [HttpGet("available-orders")]
    public async Task<IActionResult> GetAvailableOrders()
    {
        var orders = await _orderService.GetAvailableCourierOrders();
        return Ok(orders);
    }

    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var orders = await _orderService.GetCourierOrders(User.GetUserId());
        return Ok(orders);
    }

    [HttpPost("orders/{id}/accept")]
    public async Task<IActionResult> AcceptOrder(int id)
    {
        try
        {
            var order = await _orderService.AcceptCourierOrder(id, User.GetUserId());
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id}/picked-up")]
    public Task<IActionResult> MarkPickedUp(int id) =>
        DeliveryStep(() => _orderService.MarkPickedUp(id, User.GetUserId()));

    [HttpPost("orders/{id}/delivered")]
    public Task<IActionResult> MarkDelivered(int id) =>
        DeliveryStep(() => _orderService.MarkDelivered(id, User.GetUserId()));

    private async Task<IActionResult> DeliveryStep(Func<Task<Models.DTO.Output.OrderDto>> step)
    {
        try
        {
            return Ok(await step());
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
