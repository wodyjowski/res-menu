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
    public List<MenuItem> MenuItems { get; set; } = new();    public async Task<IActionResult> OnGetAsync(string? subdomain)
    {
        // Check for subdomain in query string first
        if (string.IsNullOrEmpty(subdomain))
        {
            // Then check in request items (from middleware)
            subdomain = HttpContext.Items["Subdomain"] as string;
        }

        // If still no subdomain, try to extract from hostname
        if (string.IsNullOrEmpty(subdomain))
        {
            var host = Request.Host.Host;
            // Assuming the format is subdomain.res-menu.duckdns.org
            // and not just res-menu.duckdns.org (which would be the main site)
            if (host.EndsWith(".res-menu.duckdns.org") && host != "res-menu.duckdns.org")
            {
                var parts = host.Split('.');
                if (parts.Length > 3) // e.g., mysub.res-menu.duckdns.org
                {
                    subdomain = parts[0];
                    _logger.LogInformation("Extracted subdomain '{Subdomain}' from host '{Host}'", subdomain, host);
                }
            }
            // Handle custom domains or other patterns if necessary
            // Example for *.localhost:PORT which might be used in development
            else if (host.Contains(".localhost") && !host.StartsWith("localhost")) // e.g. mysub.localhost:7000
            {
                 var parts = host.Split('.');
                 if (parts.Length > 1 && parts[0] != "localhost") // ensure it's not just localhost.someotherpart
                 {
                    subdomain = parts[0];
                    _logger.LogInformation("Extracted subdomain '{Subdomain}' from localhost host '{Host}'", subdomain, host);
                 }
            }
        }

        // Log the request details for debugging
        _logger.LogInformation(
            "Menu page accessed. Host: {Host}, Path: {Path}, Subdomain param: {Subdomain}, Subdomain from items: {SubdomainItems}",
            Request.Host.Value,
            Request.Path.Value,
            subdomain,
            HttpContext.Items["Subdomain"]
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