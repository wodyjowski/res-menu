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
        _logger.LogInformation("Attempting to resolve subdomain. Initial query param: '{InitialSubdomain}'", subdomain);

        // Check for subdomain in query string first
        if (string.IsNullOrEmpty(subdomain))
        {
            _logger.LogInformation("Query param 'subdomain' is empty. Checking HttpContext.Items[\"Subdomain\"].");
            // Then check in request items (from middleware)
            var subdomainFromItems = HttpContext.Items["Subdomain"] as string;
            if (!string.IsNullOrEmpty(subdomainFromItems))
            {
                subdomain = subdomainFromItems;
                _logger.LogInformation("Subdomain '{Subdomain}' found in HttpContext.Items.", subdomain);
            }
            else
            {
                _logger.LogInformation("HttpContext.Items[\"Subdomain\"] is also empty. Attempting to extract from hostname.");
            }
        }
        else
        {
            _logger.LogInformation("Subdomain '{Subdomain}' found in query parameter.", subdomain);
        }

        // If still no subdomain, try to extract from hostname
        if (string.IsNullOrEmpty(subdomain))
        {
            var host = Request.Host.Host;
            _logger.LogInformation("Attempting to extract subdomain from host: '{Host}'.", host);
            // Assuming the format is subdomain.res-menu.duckdns.org
            // and not just res-menu.duckdns.org (which would be the main site)
            if (host.EndsWith(".res-menu.duckdns.org") && host != "res-menu.duckdns.org")
            {
                var parts = host.Split('.');
                if (parts.Length > 2 && parts[0] != "www") // Ensure it's not www.res-menu.duckdns.org and has at least subdomain.domain.tld
                {
                    subdomain = parts[0];
                    _logger.LogInformation("Extracted subdomain '{Subdomain}' from host '{Host}' (duckdns).", subdomain, host);
                }
                else
                {
                    _logger.LogWarning("Host '{Host}' ends with .res-menu.duckdns.org but could not extract a valid subdomain part.", host);
                }
            }
            // Handle custom domains or other patterns if necessary
            // Example for *.localhost:PORT which might be used in development
            else if (host.Contains(".localhost") && !host.StartsWith("localhost")) // e.g. mysub.localhost:7000
            {
                 var parts = host.Split('.');
                 if (parts.Length > 1 && parts[0] != "localhost" && parts[0] != "www") // ensure it's not just localhost.someotherpart or www.localhost
                 {
                    subdomain = parts[0];
                    _logger.LogInformation("Extracted subdomain '{Subdomain}' from localhost host '{Host}'.", subdomain, host);
                 }
                 else
                 {
                    _logger.LogWarning("Host '{Host}' contains .localhost but could not extract a valid subdomain part.", host);
                 }
            }
            else
            {
                _logger.LogInformation("Host '{Host}' did not match known patterns for subdomain extraction.", host);
            }
        }

        _logger.LogInformation(
            "Final resolved subdomain: '{FinalSubdomain}'. Menu page accessed. Host: {HostValue}, Path: {PathValue}, Original Subdomain param: {OriginalSubdomainParam}, Subdomain from items: {SubdomainFromItemsValue}",
            subdomain,
            Request.Host.Value,
            Request.Path.Value,
            HttpContext.Request.Query["subdomain"].FirstOrDefault(), // Log original query param again for clarity
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