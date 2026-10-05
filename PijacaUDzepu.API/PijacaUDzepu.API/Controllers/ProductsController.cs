using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PijacaUDzepu.API.Controllers.Base;
using PijacaUDzepu.API.Extensions;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Controllers;

public class ProductsController : BaseApiController
{
    private readonly IProductService _productService;
    private readonly IVendorService _vendorService;

    public ProductsController(IProductService productService, IVendorService vendorService)
    {
        _productService = productService;
        _vendorService = vendorService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int? vendorId, [FromQuery] int? marketId, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var result = await _productService.GetAllProducts(search, vendorId, marketId, skip, take);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var product = await _productService.GetProductById(id);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpGet("my-products")]
    public async Task<IActionResult> GetMyProducts()
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        var products = await _productService.GetProductsByVendor(vendorId.Value);
        return Ok(products);
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(ProductInputDto dto)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        try
        {
            var product = await _productService.CreateProduct(vendorId.Value, dto);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ProductInputDto dto)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        try
        {
            var product = await _productService.UpdateProduct(id, vendorId.Value, dto);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        try
        {
            await _productService.DeleteProduct(id, vendorId.Value);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = "RequireVendorAdmin")]
    [HttpPost("{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        var vendorId = await GetCurrentVendorId();
        if (vendorId == null) return Forbid();

        if (file.Length == 0) return BadRequest(new { message = "Fajl je prazan." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Maksimalna veličina fajla je 5MB." });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowed.Contains(ext)) return BadRequest(new { message = "Dozvoljeni formati: jpg, jpeg, png, webp." });

        try
        {
            var imageUrl = await _productService.UpdateProductImage(id, vendorId.Value, file);
            return Ok(new { imageUrl });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private async Task<int?> GetCurrentVendorId()
    {
        var userId = User.GetUserId();
        if (User.IsInRole("SuperAdmin"))
        {
            // SuperAdmin can act on any vendor — but needs a vendorId from the query or from VendorUser
        }
        return await _vendorService.GetVendorIdForUser(userId);
    }
}
