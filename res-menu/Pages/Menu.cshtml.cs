using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;
using Microsoft.AspNetCore.Authorization;

namespace res_menu.Pages;

[AllowAnonymous]
public class MenuModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public MenuModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public Models.Restaurant? Restaurant { get; set; }
    public IList<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

    public async Task<IActionResult> OnGetAsync()
    {
        // Get the host from the request
        var host = Request.Host.Host.ToLower();
        var isLocalhost = host == "localhost" || host.StartsWith("127.") || host.StartsWith("192.168.");
        
        string? subdomain;
        if (isLocalhost)
        {
            // For local development, get subdomain from query string
            subdomain = Request.Query["subdomain"].ToString();
        }
        else
        {
            // In production, try to get subdomain from host first, then fallback to query string
            subdomain = host.Count(c => c == '.') > 1 
                ? host.Split('.')[0] 
                : Request.Query["subdomain"].ToString();
        }

        if (string.IsNullOrEmpty(subdomain))
        {
            return NotFound("Restaurant not found. Please check the URL.");
        }

        Restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());

        if (Restaurant == null)
        {
            return NotFound("Restaurant not found. Please check the URL.");
        }

        MenuItems = await _context.MenuItems
            .Where(m => m.RestaurantId == Restaurant.Id && m.IsAvailable)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return Page();
    }
} 