using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class DevicesController : BaseApiController
{
    private readonly IPushService _pushService;

    public DevicesController(IPushService pushService)
    {
        _pushService = pushService;
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceDto dto)
    {
        await _pushService.RegisterDevice(User.GetUserId(), dto.Token, dto.Platform);
        return NoContent();
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(RegisterDeviceDto dto)
    {
        await _pushService.UnregisterDevice(dto.Token);
        return NoContent();
    }
}
