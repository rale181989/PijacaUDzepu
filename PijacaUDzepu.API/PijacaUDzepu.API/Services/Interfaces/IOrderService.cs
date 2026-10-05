using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.Services.Interfaces;

public interface IOrderService
{
    Task<List<OrderDto>> CreateOrders(int customerId, OrderInputDto dto);
    Task<List<OrderDto>> CreateGuestOrders(GuestOrderInputDto dto);
    Task<PagedResult<OrderDto>> GetCustomerOrders(int customerId, int skip = 0, int take = 20);
    Task<PagedResult<OrderDto>> GetVendorOrders(int vendorId, int skip = 0, int take = 20);
    Task<OrderDto> GetOrderById(int orderId);
    Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus);
    Task<OrderDto> CancelOrder(int orderId, int customerId);
    Task<int> GetVendorPendingCount(int vendorId);
}
