using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;
using SkiaSharp;

namespace PijacaUDzepu.API.Services;

public class ProductService : IProductService
{
    private readonly DataContext _context;
    private readonly IWebHostEnvironment _env;

    public ProductService(DataContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task<PagedResult<ProductDto>> GetAllProducts(string? search = null, int? vendorId = null, int? marketId = null, Models.Enums.ProductCategory? category = null, int skip = 0, int take = 20)
    {
        var query = _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .Where(p => p.IsAvailable && p.Vendor.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            var categoryMatch = MatchCategory(search);
            query = query.Where(p =>
                EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern)) ||
                EF.Functions.ILike(EF.Functions.Unaccent(p.Vendor.Name), EF.Functions.Unaccent(pattern)) ||
                EF.Functions.ILike(EF.Functions.Unaccent(p.Vendor.Market!.Name), EF.Functions.Unaccent(pattern)) ||
                (p.Vendor.Stall != null && EF.Functions.ILike(p.Vendor.Stall.Label, pattern)) ||
                (categoryMatch != null && p.Category == categoryMatch));
        }

        if (marketId.HasValue)
            query = query.Where(p => p.Vendor.MarketId == marketId.Value);

        if (vendorId.HasValue)
            query = query.Where(p => p.VendorId == vendorId.Value);

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<List<ProductDto>> GetProductsByVendor(int vendorId)
    {
        return await _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .Where(p => p.VendorId == vendorId)
            .OrderBy(p => p.Name)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProductDto> GetProductById(int id)
    {
        var product = await _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");
        return MapToDto(product);
    }

    public async Task<ProductDto> CreateProduct(int vendorId, ProductInputDto dto)
    {
        var vendor = await _context.Vendors.FindAsync(vendorId)
            ?? throw new KeyNotFoundException("Prodavac nije pronađen.");

        var product = new Product
        {
            VendorId = vendorId,
            Name = dto.Name,
            Price = dto.Price,
            Unit = dto.Unit,
            Category = dto.Category,
            Note = dto.Note,
            IsAvailable = dto.IsAvailable
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        product.Vendor = vendor;
        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateProduct(int productId, int vendorId, ProductInputDto dto)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.Id == productId && p.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");

        product.Name = dto.Name;
        product.Price = dto.Price;
        product.Unit = dto.Unit;
        product.Category = dto.Category;
        product.Note = dto.Note;
        product.IsAvailable = dto.IsAvailable;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(product);
    }

    public async Task DeleteProduct(int productId, int vendorId)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
    }

    public async Task<string> UpdateProductImage(int productId, int vendorId, IFormFile file)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");

        var fileName = $"product_{productId}_{Guid.NewGuid()}.jpg";
        var filePath = Path.Combine(_env.WebRootPath, "Resources", "Images", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var original = SKBitmap.Decode(memoryStream);
        if (original == null)
            throw new InvalidOperationException("Neispravan format slike.");

        const int maxWidth = 800;
        const int maxHeight = 600;

        var bitmap = original;
        if (original.Width > maxWidth || original.Height > maxHeight)
        {
            var ratioX = (float)maxWidth / original.Width;
            var ratioY = (float)maxHeight / original.Height;
            var ratio = Math.Min(ratioX, ratioY);
            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);
            bitmap = original.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        data.SaveTo(fileStream);

        if (bitmap != original) bitmap.Dispose();

        DeleteOldImage(product.ImageUrl);

        product.ImageUrl = $"/Resources/Images/{fileName}";
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return product.ImageUrl;
    }

    private void DeleteOldImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || imageUrl.StartsWith("http"))
            return;

        var oldPath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        if (File.Exists(oldPath)) File.Delete(oldPath);
    }

    private static readonly Dictionary<string, Models.Enums.ProductCategory> CategoryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["voce"] = Models.Enums.ProductCategory.Voce,
        ["voće"] = Models.Enums.ProductCategory.Voce,
        ["povrce"] = Models.Enums.ProductCategory.Povrce,
        ["povrće"] = Models.Enums.ProductCategory.Povrce,
        ["mlecni proizvodi"] = Models.Enums.ProductCategory.MlecniProizvodi,
        ["mlečni proizvodi"] = Models.Enums.ProductCategory.MlecniProizvodi,
        ["mesni proizvodi"] = Models.Enums.ProductCategory.MesniProizvodi,
        ["konditori"] = Models.Enums.ProductCategory.Konditori,
        ["kucna hemija"] = Models.Enums.ProductCategory.KucnaHemija,
        ["kućna hemija"] = Models.Enums.ProductCategory.KucnaHemija,
        ["ostalo"] = Models.Enums.ProductCategory.Ostalo,
    };

    private static Models.Enums.ProductCategory? MatchCategory(string search)
    {
        var s = search.Trim();
        foreach (var kvp in CategoryLabels)
        {
            if (kvp.Key.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                s.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        VendorId = p.VendorId,
        VendorName = p.Vendor?.Name ?? "",
        MarketId = p.Vendor?.MarketId ?? 0,
        MarketName = p.Vendor?.Market?.Name ?? "",
        StallLabel = p.Vendor?.Stall?.Label,
        Name = p.Name,
        Price = p.Price,
        Unit = p.Unit,
        Category = p.Category,
        Note = p.Note,
        ImageUrl = p.ImageUrl,
        IsAvailable = p.IsAvailable
    };
}
