using System.ComponentModel.DataAnnotations;
using res_menu.Validation;

namespace res_menu.Models;

public class MenuItem
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Menu item name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Menu item name must be between 2 and 100 characters")]
    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Description")]
    public string? Description { get; set; }
      [Required(ErrorMessage = "Price is required")]
    [PriceValidation(MinValue = 0.01, MaxValue = 10000)]
    [Display(Name = "Price")]
    public decimal Price { get; set; }
    
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }
    
    [Display(Name = "Available")]
    public bool IsAvailable { get; set; } = true;
    
    [StringLength(50, ErrorMessage = "Category name cannot exceed 50 characters")]
    [Display(Name = "Category")]
    public string? Category { get; set; }
    
    [Required(ErrorMessage = "Restaurant ID is required")]
    public int RestaurantId { get; set; }
    
    public virtual Restaurant? Restaurant { get; set; }
}