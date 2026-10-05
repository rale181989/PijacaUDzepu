using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class VendorsController : BaseApiController
{
    private readonly IVendorService _vendorService;

    public VendorsController(IVendorService vendorService)
    {
        _vendorService = vendorService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var vendors = await _vendorService.GetAllVendors();
        return Ok(vendors);
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAllIncludingInactive()
    {
        var vendors = await _vendorService.GetAllVendors(includeInactive: true);
        return Ok(vendors);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var vendor = await _vendorService.GetVendorById(id);
            return Ok(vendor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(VendorInputDto dto)
    {
        var vendor = await _vendorService.CreateVendor(dto);
        return CreatedAtAction(nameof(GetById), new { id = vendor.Id }, vendor);
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, VendorInputDto dto)
    {
        try
        {
            var vendor = await _vendorService.UpdateVendor(id, dto);
            return Ok(vendor);
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
            await _vendorService.ToggleVendorActive(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("my-vendor")]
    public async Task<IActionResult> GetMyVendor()
    {
        var userId = User.GetUserId();
        var vendorId = await _vendorService.GetVendorIdForUser(userId);
        if (vendorId == null) return NotFound(new { message = "Niste dodeljeni prodavcu." });

        var vendor = await _vendorService.GetVendorById(vendorId.Value);
        return Ok(vendor);
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpPut("my-vendor")]
    public async Task<IActionResult> UpdateMyVendor(VendorInputDto dto)
    {
        var userId = User.GetUserId();
        var vendorId = await _vendorService.GetVendorIdForUser(userId);
        if (vendorId == null) return NotFound(new { message = "Niste dodeljeni prodavcu." });

        try
        {
            var vendor = await _vendorService.UpdateVendor(vendorId.Value, dto);
            return Ok(vendor);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireSuperAdmin")]
    [HttpPost("{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { message = "Fajl je prazan." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Maksimalna veličina fajla je 5MB." });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowed.Contains(ext)) return BadRequest(new { message = "Dozvoljeni formati: jpg, jpeg, png, webp." });

        try
        {
            var imageUrl = await _vendorService.UpdateVendorImage(id, file);
            return Ok(new { imageUrl });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
