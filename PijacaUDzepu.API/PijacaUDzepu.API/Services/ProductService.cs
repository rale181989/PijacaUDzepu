using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Helpers;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class ProductService : IProductService
{
    private readonly DataContext _context;
    private readonly IImageStorage _images;

    public ProductService(DataContext context, IImageStorage images)
    {
        _context = context;
        _images = images;
    }

    public async Task<PagedResult<ProductDto>> GetAllProducts(string? search = null, int? vendorId = null, int? marketId = null, Models.Enums.ProductCategory? category = null, Models.Enums.ProductUnit? unit = null, Models.Enums.ProductSort sort = Models.Enums.ProductSort.NameAsc, int skip = 0, int take = 20)
    {
        var query = _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .Where(p => p.IsAvailable && p.Vendor.IsActive)
            .AsQueryable();

        // Pretraga je samo po nazivu proizvoda; pijaca, prodavac, tezga i grupa imaju svoje filtere
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern)));
        }

        if (marketId.HasValue)
            query = query.Where(p => p.Vendor.MarketId == marketId.Value);

        if (vendorId.HasValue)
            query = query.Where(p => p.VendorId == vendorId.Value);

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        // Filter "Kg" obuhvata i proizvode na gram, jer se cene mogu porediti po kilogramu
        if (unit == Models.Enums.ProductUnit.Kg || unit == Models.Enums.ProductUnit.Gram)
            query = query.Where(p => p.Unit == Models.Enums.ProductUnit.Kg || p.Unit == Models.Enums.ProductUnit.Gram);
        else if (unit.HasValue)
            query = query.Where(p => p.Unit == unit.Value);

        var totalCount = await query.CountAsync();

        // Id kao drugi ključ da bi straničenje bilo stabilno kada više proizvoda ima istu cenu/naziv
        IOrderedQueryable<Product> ordered = sort switch
        {
            Models.Enums.ProductSort.NameDesc => query.OrderByDescending(p => p.Name),
            Models.Enums.ProductSort.PriceAsc => query.OrderBy(PricePerComparableUnit).ThenBy(p => p.Name),
            Models.Enums.ProductSort.PriceDesc => query.OrderByDescending(PricePerComparableUnit).ThenBy(p => p.Name),
            Models.Enums.ProductSort.Newest => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderBy(p => p.Name)
        };

        var items = await ordered
            .ThenBy(p => p.Id)
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
            ProductClass = dto.ProductClass,
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
        product.ProductClass = dto.ProductClass;
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

        await _images.Delete(product.ImageUrl);
    }

    public async Task<string> UpdateProductImage(int productId, int vendorId, IFormFile file)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");

        var jpeg = await ImageResizer.ToJpeg(file);
        var newUrl = await _images.Save(jpeg, $"products/product_{productId}_{Guid.NewGuid()}.jpg", ImageResizer.ContentType);

        // Stara slika se briše tek kad baza pokazuje na novu; ako čuvanje padne, briše se nova
        var oldUrl = product.ImageUrl;
        product.ImageUrl = newUrl;
        product.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            await _images.Delete(newUrl);
            throw;
        }

        await _images.Delete(oldUrl);
        return newUrl;
    }

    // Cena na gram se preračunava u cenu po kg da bi se pri sortiranju poredila sa proizvodima na kg
    private static readonly System.Linq.Expressions.Expression<Func<Product, decimal>> PricePerComparableUnit =
        p => p.Unit == Models.Enums.ProductUnit.Gram ? p.Price * 1000 : p.Price;

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
        ProductClass = p.ProductClass,
        Note = p.Note,
        ImageUrl = p.ImageUrl,
        IsAvailable = p.IsAvailable
    };
}
