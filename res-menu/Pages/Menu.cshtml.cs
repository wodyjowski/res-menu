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
    private readonly ILogger<MenuModel> _logger;

    public MenuModel(ApplicationDbContext context, ILogger<MenuModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public res_menu.Models.Restaurant? Restaurant { get; set; }
    public List<MenuItem> MenuItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? subdomain)
    {
        // Log the request details for debugging
        _logger.LogInformation(
            "Menu page accessed. Host: {Host}, Path: {Path}, Subdomain param: {Subdomain}",
            Request.Host.Value,
            Request.Path.Value,
            subdomain
        );

        if (string.IsNullOrEmpty(subdomain))
        {
            _logger.LogWarning("No subdomain provided");
            return NotFound("No subdomain provided");
        }

        Restaurant = await _context.Restaurants
            .Include(r => r.MenuItems)
            .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());

        if (Restaurant == null)
        {
            _logger.LogWarning("Restaurant not found for subdomain: {Subdomain}", subdomain);
            return NotFound($"Restaurant not found for subdomain: {subdomain}");
        }

        MenuItems = Restaurant.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToList();

        return Page();
    }
} 