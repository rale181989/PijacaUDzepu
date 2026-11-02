using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Output;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IFavoriteService
{
    Task<FavoritesDto> Get(int userId);
    Task SetProduct(int userId, int productId, bool favorite);
    Task SetVendor(int userId, int vendorId, bool favorite);
    Task<FavoritesDto> Sync(int userId, List<int> productIds, List<int> vendorIds);
    Task SetStockAlert(int userId, int productId, bool enabled);
    Task NotifyBackInStock(Product product, string vendorName);
}
