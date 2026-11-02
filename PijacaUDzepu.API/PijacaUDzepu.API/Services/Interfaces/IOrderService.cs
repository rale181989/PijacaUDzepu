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
    Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus, string? message = null);
    decimal CourierFee { get; }
    Task<OrderDto> CancelOrder(int orderId, int customerId);
    Task<List<OrderDto>> GetTrackedOrders(string code);
    Task<OrderDto> RateOrder(int orderId, int? customerId, string? trackingCode, OrderRatingInputDto dto);
    Task<OrderDto> CancelTrackedOrder(string code, int orderId);
    Task<int> GetVendorPendingCount(int vendorId);
    Task<VendorTodayDto> GetVendorToday(int vendorId);
    Task<List<OrderDto>> GetAvailableCourierOrders(int courierId);
    Task<PagedResult<OrderDto>> GetCourierOrders(int courierId, bool finished, int skip = 0, int take = 20);
    Task<OrderDto> AcceptCourierOrder(int orderId, int courierId);
    Task DeclineCourierOrder(int orderId, int courierId);
    Task<List<OrderDto>> AcceptCourierCheckout(Guid checkoutId, int courierId);
    Task<CourierSettlementDto> GetCourierSettlement(int courierId, DateOnly date);
    Task<OrderDto> MarkPickedUp(int orderId, int courierId);
    Task<OrderDto> MarkDelivered(int orderId, int courierId);
}
