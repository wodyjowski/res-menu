using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;
using res_menu.Services;

namespace res_menu.Pages;

[AllowAnonymous]
public class MenuModel : PageModel
{    private readonly ApplicationDbContext _context;
    private readonly IOrderService _orderService;
    private readonly ILogger<MenuModel> _logger;

    public MenuModel(ApplicationDbContext context, IOrderService orderService, ILogger<MenuModel> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }    public res_menu.Models.Restaurant? Restaurant { get; set; }
    public List<MenuItem> MenuItems { get; set; } = new();
    
    [BindProperty]
    public OrderViewModel OrderForm { get; set; } = new();
    
    public class OrderViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? TableNumber { get; set; }
        public string? Notes { get; set; }
        public List<OrderItemViewModel> Items { get; set; } = new();
    }
    
    public class OrderItemViewModel
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public string? SpecialInstructions { get; set; }
    }public async Task<IActionResult> OnGetAsync(string? subdomain)
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
      public async Task<IActionResult> OnPostCreateOrderAsync(string? subdomain)
    {
        _logger.LogInformation("Order creation started. Initial subdomain parameter: '{Subdomain}'", subdomain);
        
        // Load restaurant data first - use the same subdomain resolution logic as OnGetAsync
        if (string.IsNullOrEmpty(subdomain))
        {
            // Check query parameter
            subdomain = HttpContext.Request.Query["subdomain"].FirstOrDefault();
            _logger.LogInformation("Subdomain from query parameter: '{Subdomain}'", subdomain);
        }
        
        if (string.IsNullOrEmpty(subdomain))
        {
            // Check HttpContext.Items (set by middleware)
            subdomain = HttpContext.Items["Subdomain"] as string;
            _logger.LogInformation("Subdomain from HttpContext.Items: '{Subdomain}'", subdomain);
        }
        
        if (string.IsNullOrEmpty(subdomain))
        {
            _logger.LogError("No subdomain provided in order creation");
            return BadRequest("No subdomain provided");
        }

        _logger.LogInformation("Final subdomain for order creation: '{Subdomain}'", subdomain);

        Restaurant = await _context.Restaurants
            .Include(r => r.MenuItems)
            .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());

        if (Restaurant == null)
        {
            _logger.LogError("Restaurant not found for subdomain: {Subdomain}", subdomain);
            return NotFound($"Restaurant not found for subdomain: {subdomain}");
        }

        MenuItems = Restaurant.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }        try
        {
            var order = new res_menu.Models.Order
            {
                CustomerName = OrderForm.CustomerName,
                CustomerPhone = OrderForm.CustomerPhone,
                CustomerEmail = OrderForm.CustomerEmail,
                TableNumber = OrderForm.TableNumber,
                Notes = OrderForm.Notes,
                RestaurantId = Restaurant.Id,
                CustomerOrderId = Guid.NewGuid().ToString()
            };

            foreach (var item in OrderForm.Items.Where(i => i.Quantity > 0))
            {
                var menuItem = Restaurant.MenuItems.FirstOrDefault(m => m.Id == item.MenuItemId);
                if (menuItem != null)
                {
                    order.OrderItems.Add(new OrderItem
                    {
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        UnitPrice = menuItem.Price,
                        SpecialInstructions = item.SpecialInstructions
                    });
                }
            }

            if (!order.OrderItems.Any())
            {
                ModelState.AddModelError("", "Please select at least one item to order.");
                return Page();
            }

            var createdOrder = await _orderService.CreateOrderAsync(order);
            
            // Store the customer order ID in a cookie for tracking
            Response.Cookies.Append("CustomerOrderId", createdOrder.CustomerOrderId, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });

            TempData["SuccessMessage"] = "Your order has been placed successfully!";
            return RedirectToPage("/Order/Status", new { customerOrderId = createdOrder.CustomerOrderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            ModelState.AddModelError("", "An error occurred while placing your order. Please try again.");
            return Page();
        }
    }
}