using System.ComponentModel.DataAnnotations;

namespace res_menu.Models;

public class Order
{
    public int Id { get; set; }
    
    [Required]
    public string CustomerName { get; set; } = string.Empty;
    
    public string? CustomerPhone { get; set; }
    
    public string? CustomerEmail { get; set; }
    
    public string? TableNumber { get; set; }
    
    public string? Notes { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    
    [Required]
    public decimal TotalAmount { get; set; }
    
    [Required]
    public int RestaurantId { get; set; }
    
    public virtual Restaurant? Restaurant { get; set; }
    
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
      // Unique identifier for customer tracking (stored in cookie)
    [Required]
    public string CustomerOrderId { get; set; } = Guid.NewGuid().ToString();
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Preparing = 2,
    Ready = 3,
    Delivered = 4,
    Cancelled = 5
}