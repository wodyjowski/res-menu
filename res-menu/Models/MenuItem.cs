using System.ComponentModel.DataAnnotations;

namespace res_menu.Models;

public class MenuItem
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [Range(0, 10000)]
    public decimal Price { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public bool IsAvailable { get; set; } = true;
    
    public string? Category { get; set; }
    
    [Required]
    public int RestaurantId { get; set; }
    
    public virtual Restaurant? Restaurant { get; set; }
} 