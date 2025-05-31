using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        try
        {
            // Calculate total amount
            order.TotalAmount = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
            order.CreatedAt = DateTime.UtcNow;
            order.Status = OrderStatus.Pending;
            
            if (string.IsNullOrEmpty(order.CustomerOrderId))
            {
                order.CustomerOrderId = Guid.NewGuid().ToString();
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Order created with ID: {OrderId}, Customer Order ID: {CustomerOrderId}", 
                order.Id, order.CustomerOrderId);
            
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.Restaurant)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetOrderByCustomerOrderIdAsync(string customerOrderId)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.Restaurant)
            .FirstOrDefaultAsync(o => o.CustomerOrderId == customerOrderId);
    }

    public async Task<List<Order>> GetOrdersByRestaurantIdAsync(int restaurantId)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetOrdersByRestaurantAndStatusAsync(int restaurantId, OrderStatus? status = null)
    {
        var query = _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .Where(o => o.RestaurantId == restaurantId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> UpdateOrderStatusAsync(int orderId, OrderStatus status)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        order.Status = status;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, status);
        
        return order;
    }

    public async Task<bool> DeleteOrderAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Order {OrderId} deleted", id);
        
        return true;
    }
}
