using System.ComponentModel.DataAnnotations;

namespace res_menu.Models;

public class OrderItem
{
    public int Id { get; set; }
    
    [Required]
    public int OrderId { get; set; }
    
    public virtual Order? Order { get; set; }
    
    [Required]
    public int MenuItemId { get; set; }
    
    public virtual MenuItem? MenuItem { get; set; }
    
    [Required]
    [Range(1, 99)]
    public int Quantity { get; set; }
    
    [Required]
    public decimal UnitPrice { get; set; }
    
    public string? SpecialInstructions { get; set; }
    
    // Total price for this order item (UnitPrice * Quantity)
    public decimal TotalPrice => UnitPrice * Quantity;
}
