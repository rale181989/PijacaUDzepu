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
    private readonly IFavoriteService _favorites;

    public ProductService(DataContext context, IImageStorage images, IFavoriteService favorites)
    {
        _context = context;
        _images = images;
        _favorites = favorites;
    }

    public async Task<PagedResult<ProductDto>> GetAllProducts(string? search = null, int? vendorId = null, int? marketId = null, Models.Enums.ProductCategory? category = null, Models.Enums.ProductUnit? unit = null, Models.Enums.ProductSort sort = Models.Enums.ProductSort.NameAsc, int skip = 0, int take = 20, List<int>? ids = null, List<int>? vendorIds = null)
    {
        var query = _context.Products
            .Include(p => p.Vendor).ThenInclude(v => v.Market)
            .Include(p => p.Vendor).ThenInclude(v => v.Stall)
            .Where(p => p.Vendor.IsActive)
            .AsQueryable();

        // Filter "Omiljeni": samo izabrani proizvodi i dostupni proizvodi omiljenih prodavaca. Tu se vide i
        // trenutno nedostupni omiljeni proizvodi (kupac traži obaveštenje kad budu dostupni); inače samo dostupni.
        if ((ids?.Count ?? 0) > 0 || (vendorIds?.Count ?? 0) > 0)
        {
            var favoriteProducts = ids ?? new List<int>();
            var favoriteVendors = vendorIds ?? new List<int>();
            query = query.Where(p => favoriteProducts.Contains(p.Id) || (favoriteVendors.Contains(p.VendorId) && p.IsAvailable));
        }
        else
        {
            query = query.Where(p => p.IsAvailable);
        }

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

        await FillVendorRatings(items);

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
            StockQuantity = dto.StockQuantity,
            // Proizvod bez robe na stanju nije u ponudi
            IsAvailable = dto.IsAvailable && dto.StockQuantity is null or > 0
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

        var wasAvailable = product.IsAvailable;
        product.Name = dto.Name;
        product.Price = dto.Price;
        product.Unit = dto.Unit;
        product.Category = dto.Category;
        product.ProductClass = dto.ProductClass;
        product.Note = dto.Note;
        product.StockQuantity = dto.StockQuantity;
        product.IsAvailable = dto.IsAvailable && dto.StockQuantity is null or > 0;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Proizvod je ponovo u ponudi: javi kupcima koji su tražili obaveštenje
        if (!wasAvailable && product.IsAvailable)
            await _favorites.NotifyBackInStock(product, product.Vendor.Name);

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

    // Brza izmena cene, stanja ili dostupnosti sa liste proizvoda
    public async Task<ProductDto> QuickUpdate(int productId, int vendorId, QuickProductUpdateDto dto)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.Id == productId && p.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Proizvod nije pronađen.");

        var wasAvailable = product.IsAvailable;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.ClearStock) product.StockQuantity = null;
        else if (dto.StockQuantity.HasValue)
        {
            // Nova roba na stanju vraća proizvod u ponudu, nula ga skida
            if (product.StockQuantity == 0 && !product.IsAvailable && dto.StockQuantity > 0)
                product.IsAvailable = true;
            product.StockQuantity = dto.StockQuantity.Value;
        }
        if (dto.IsAvailable.HasValue) product.IsAvailable = dto.IsAvailable.Value;
        if (product.StockQuantity == 0) product.IsAvailable = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (!wasAvailable && product.IsAvailable)
            await _favorites.NotifyBackInStock(product, product.Vendor.Name);
        return MapToDto(product);
    }

    // Prosečna ocena prodavca (iz ocena porudžbina) za sve proizvode na strani, jednim upitom
    private async Task FillVendorRatings(List<ProductDto> items)
    {
        var vendorIds = items.Select(i => i.VendorId).Distinct().ToList();
        var ratings = await _context.OrderRatings
            .Where(r => vendorIds.Contains(r.Order.VendorId))
            .GroupBy(r => r.Order.VendorId)
            .Select(g => new { VendorId = g.Key, Average = g.Average(r => (double)r.VendorScore), Count = g.Count() })
            .ToDictionaryAsync(r => r.VendorId);
        foreach (var item in items)
        {
            if (!ratings.TryGetValue(item.VendorId, out var r)) continue;
            item.VendorRating = Math.Round(r.Average, 1);
            item.VendorRatingCount = r.Count;
        }
    }

    // Cena na gram se preračunava u cenu po kg da bi se pri sortiranju poredila sa proizvodima na kg
    private static readonly System.Linq.Expressions.Expression<Func<Product, decimal>> PricePerComparableUnit =
        p => p.Unit == Models.Enums.ProductUnit.Gram ? p.Price * 1000 : p.Price;

    private static ProductDto MapToDto(Product p)
    {
        var status = p.Vendor != null ? VendorAvailability.Evaluate(p.Vendor) : null;
        return new ProductDto
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
        IsAvailable = p.IsAvailable,
        StockQuantity = p.StockQuantity,
        VendorOpen = status?.IsOpen ?? true,
        VendorClosedReason = status?.ClosedReason,
        VendorOpensAt = status?.OpensAt
        };
    }
}
