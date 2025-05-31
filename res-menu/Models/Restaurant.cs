using System.ComponentModel.DataAnnotations;

namespace res_menu.Models;

public class Restaurant
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9-]+$", ErrorMessage = "Subdomain can only contain letters, numbers, and hyphens")]
    public string Subdomain { get; set; } = string.Empty;
    
    public string OwnerId { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string? LogoUrl { get; set; }
    
    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
} 