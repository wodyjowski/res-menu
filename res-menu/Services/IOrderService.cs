using res_menu.Models;

namespace res_menu.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> GetOrderByIdAsync(int id);
    Task<Order?> GetOrderByCustomerOrderIdAsync(string customerOrderId);
    Task<List<Order>> GetOrdersByRestaurantIdAsync(int restaurantId);
    Task<Order> UpdateOrderStatusAsync(int orderId, OrderStatus status);
    Task<List<Order>> GetOrdersByRestaurantAndStatusAsync(int restaurantId, OrderStatus? status = null);
    Task<bool> DeleteOrderAsync(int id);
}
