using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

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
    public List<MenuItem> MenuItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? subdomain)
    {
        if (string.IsNullOrEmpty(subdomain))
        {
            return NotFound();
        }

        Restaurant = await _context.Restaurants
            .Include(r => r.MenuItems)
            .FirstOrDefaultAsync(r => r.Subdomain == subdomain);

        if (Restaurant == null)
        {
            return NotFound();
        }

        MenuItems = Restaurant.MenuItems.OrderBy(m => m.Name).ToList();
        return Page();
    }
} 