using System.ComponentModel.DataAnnotations;
using res_menu.Validation;

namespace res_menu.Models;

public class Restaurant
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Restaurant name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Restaurant name must be between 2 and 100 characters")]
    [Display(Name = "Restaurant Name")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Subdomain is required")]
    [SubdomainValidation]
    [Display(Name = "Subdomain")]
    public string Subdomain { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Owner ID is required")]
    public string OwnerId { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Restaurant Description")]
    public string? Description { get; set; }
    
    [Display(Name = "Logo URL")]
    public string? LogoUrl { get; set; }
    
    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
} 