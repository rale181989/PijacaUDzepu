using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllProducts(string? search = null, int? vendorId = null, int? marketId = null, ProductCategory? category = null, int skip = 0, int take = 20);
    Task<List<ProductDto>> GetProductsByVendor(int vendorId);
    Task<ProductDto> GetProductById(int id);
    Task<ProductDto> CreateProduct(int vendorId, ProductInputDto dto);
    Task<ProductDto> UpdateProduct(int productId, int vendorId, ProductInputDto dto);
    Task DeleteProduct(int productId, int vendorId);
    Task<string> UpdateProductImage(int productId, int vendorId, IFormFile file);
}
