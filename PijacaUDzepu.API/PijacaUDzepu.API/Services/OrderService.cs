using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Models.Enums;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class OrderService : IOrderService
{
    private readonly DataContext _context;
    private readonly IHubContext<OrderHub> _hubContext;

    public OrderService(DataContext context, IHubContext<OrderHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<List<OrderDto>> CreateOrders(int customerId, OrderInputDto dto)
    {
        var orders = await BuildOrders(dto.Items, dto.Note);
        foreach (var order in orders)
            order.CustomerId = customerId;

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        await NotifyVendorsNewOrder(orders);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderDto>> CreateGuestOrders(GuestOrderInputDto dto)
    {
        var orders = await BuildOrders(dto.Items, dto.Note);
        foreach (var order in orders)
        {
            order.GuestName = dto.GuestName.Trim();
            order.GuestPhone = dto.GuestPhone.Trim();
            order.GuestAddress = dto.GuestAddress?.Trim();
        }

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();

        await NotifyVendorsNewOrder(orders);

        return orders.Select(MapToDto).ToList();
    }

    private async Task<List<Order>> BuildOrders(List<OrderItemInputDto> items, string? note)
    {
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Include(p => p.Vendor)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var unavailable = products.Where(p => !p.IsAvailable || !p.Vendor.IsActive).ToList();
        if (unavailable.Any())
            throw new InvalidOperationException(
                $"Sledeći proizvodi nisu dostupni: {string.Join(", ", unavailable.Select(p => p.Name))}");

        var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();
        if (missingIds.Any())
            throw new KeyNotFoundException($"Proizvodi sa ID {string.Join(", ", missingIds)} nisu pronađeni.");

        var groupedByVendor = items
            .GroupBy(item => products.First(p => p.Id == item.ProductId).VendorId)
            .ToList();

        var orders = new List<Order>();

        foreach (var group in groupedByVendor)
        {
            var order = new Order
            {
                VendorId = group.Key,
                Note = note,
                Status = OrderStatus.Pending,
                Items = new List<OrderItem>()
            };

            foreach (var item in group)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var totalPrice = product.Price * item.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    Unit = product.Unit,
                    UnitPrice = product.Price,
                    TotalPrice = totalPrice
                });
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
            orders.Add(order);
        }

        return orders;
    }

    public async Task<PagedResult<OrderDto>> GetCustomerOrders(int customerId, int skip = 0, int take = 20)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Vendor)
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<PagedResult<OrderDto>> GetVendorOrders(int vendorId, int skip = 0, int take = 20)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Vendor)
            .Include(o => o.Customer)
            .Where(o => o.VendorId == vendorId)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }

    public async Task<OrderDto> GetOrderById(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Vendor)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        return MapToDto(order);
    }

    public async Task<OrderDto> UpdateOrderStatus(int orderId, int vendorId, OrderStatus newStatus)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Vendor)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.VendorId == vendorId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        ValidateStatusTransition(order.Status, newStatus);

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (order.CustomerId.HasValue)
        {
            await _hubContext.Clients.Group($"user_{order.CustomerId.Value}")
                .SendAsync("OrderStatusChanged", new
                {
                    orderId = order.Id,
                    status = newStatus.ToString(),
                    vendorName = order.Vendor.Name
                });
        }

        return MapToDto(order);
    }

    public async Task<OrderDto> CancelOrder(int orderId, int customerId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Vendor)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId)
            ?? throw new KeyNotFoundException("Porudžbina nije pronađena.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Samo porudžbine na čekanju mogu biti otkazane.");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return MapToDto(order);
    }

    public async Task<int> GetVendorPendingCount(int vendorId)
    {
        return await _context.Orders
            .CountAsync(o => o.VendorId == vendorId && o.Status == OrderStatus.Pending);
    }

    private async Task NotifyVendorsNewOrder(List<Order> orders)
    {
        foreach (var order in orders)
        {
            var customerName = order.Customer != null
                ? $"{order.Customer.FirstName} {order.Customer.LastName}"
                : order.GuestName ?? "Gost";

            await _hubContext.Clients.Group($"vendor_{order.VendorId}")
                .SendAsync("NewOrder", new
                {
                    orderId = order.Id,
                    customerName,
                    totalAmount = order.TotalAmount,
                    itemCount = order.Items.Count
                });
        }
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus next)
    {
        var valid = (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Rejected) => true,
            (OrderStatus.Confirmed, OrderStatus.ReadyForPickup) => true,
            (OrderStatus.ReadyForPickup, OrderStatus.Completed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException(
                $"Nije moguć prelaz statusa iz '{current}' u '{next}'.");
    }

    private static OrderDto MapToDto(Order o) => new()
    {
        Id = o.Id,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer != null
            ? $"{o.Customer.FirstName} {o.Customer.LastName}"
            : o.GuestName ?? "Gost",
        VendorId = o.VendorId,
        VendorName = o.Vendor.Name,
        Status = o.Status,
        TotalAmount = o.TotalAmount,
        Note = o.Note,
        CreatedAt = o.CreatedAt,
        IsGuestOrder = o.CustomerId == null,
        GuestPhone = o.GuestPhone,
        GuestAddress = o.GuestAddress,
        Items = o.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            Unit = i.Unit,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice
        }).ToList()
    };
}
