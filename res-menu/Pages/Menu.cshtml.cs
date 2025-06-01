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
{    
    private readonly ApplicationDbContext _context;
    private readonly IOrderService _orderService;
    private readonly ILogger<MenuModel> _logger;

    public MenuModel(ApplicationDbContext context, IOrderService orderService, ILogger<MenuModel> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }    

    public res_menu.Models.Restaurant? Restaurant { get; set; }
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
    }

    private async Task<IActionResult> HandleRestaurantNotFound(string? subdomain)
    {
        _logger.LogWarning("Restaurant not found. Subdomain: {Subdomain}, Host: {Host}, Path: {Path}",
            subdomain,
            Request.Host.Value,
            Request.Path.Value);
            
        return NotFound("The restaurant you're looking for doesn't exist or the URL might be incorrect.");
    }

    private async Task<Models.Restaurant?> GetRestaurantBySubdomain(string subdomain)
    {
        return await _context.Restaurants
            .Include(r => r.MenuItems)
            .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());
    }    public async Task<IActionResult> OnGetAsync(string? subdomain)
    {
        try
        {
            _logger.LogInformation("OnGetAsync called with subdomain parameter: {Subdomain}", subdomain);
            _logger.LogInformation("HttpContext.Items['Subdomain']: {SubdomainItem}", HttpContext.Items["Subdomain"]);
            _logger.LogInformation("Query string subdomain: {QuerySubdomain}", HttpContext.Request.Query["subdomain"].FirstOrDefault());
            
            if (string.IsNullOrEmpty(subdomain))
            {
                subdomain = HttpContext.Items["Subdomain"] as string;
                _logger.LogInformation("Using subdomain from HttpContext.Items: {Subdomain}", subdomain);
            }
            
            if (string.IsNullOrEmpty(subdomain))
            {
                subdomain = HttpContext.Request.Query["subdomain"].FirstOrDefault();
                _logger.LogInformation("Using subdomain from query string: {Subdomain}", subdomain);
            }
            
            if (string.IsNullOrEmpty(subdomain))
            {
                _logger.LogWarning("No subdomain found in any source");
                return await HandleRestaurantNotFound(subdomain);
            }

            _logger.LogInformation("Looking for restaurant with subdomain: {Subdomain}", subdomain);
            
            // Explicitly include MenuItems and ensure they are loaded
            Restaurant = await _context.Restaurants
                .Include(r => r.MenuItems)
                .AsNoTracking()  // For better performance since we're only reading
                .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());

            if (Restaurant == null)
            {
                _logger.LogWarning("Restaurant not found for subdomain: {Subdomain}", subdomain);
                return await HandleRestaurantNotFound(subdomain);
            }

            _logger.LogInformation("Found restaurant: {RestaurantName} with {MenuItemCount} menu items", 
                Restaurant.Name, 
                Restaurant.MenuItems?.Count ?? 0);

            // Initialize MenuItems as an empty list if null
            MenuItems = Restaurant.MenuItems?.ToList() ?? new List<MenuItem>();
            
            _logger.LogInformation("Loaded {MenuItemCount} menu items", MenuItems.Count);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading menu for subdomain: {Subdomain}", subdomain);
            throw; // Let the global error handler deal with it
        }
    }

    public async Task<IActionResult> OnPostCreateOrderAsync(string? subdomain)
    {
        if (string.IsNullOrEmpty(subdomain))
        {
            subdomain = HttpContext.Items["Subdomain"] as string;
        }
        
        if (string.IsNullOrEmpty(subdomain))
        {
            return await HandleRestaurantNotFound(subdomain);
        }

        Restaurant = await GetRestaurantBySubdomain(subdomain);

        if (Restaurant == null)
        {
            return await HandleRestaurantNotFound(subdomain);
        }

        MenuItems = Restaurant.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var order = new res_menu.Models.Order
            {
                CustomerName = OrderForm.CustomerName,
                CustomerPhone = OrderForm.CustomerPhone,
                CustomerEmail = OrderForm.CustomerEmail,
                TableNumber = OrderForm.TableNumber,
                Notes = OrderForm.Notes,
                RestaurantId = Restaurant.Id,
                CustomerOrderId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending
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

            order.TotalAmount = order.OrderItems.Sum(item => item.UnitPrice * item.Quantity);
            var createdOrder = await _orderService.CreateOrderAsync(order);
            
            // Store the customer order ID in a cookie for tracking
            Response.Cookies.Append("CustomerOrderId", createdOrder.CustomerOrderId, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });            TempData["SuccessMessage"] = "Your order has been placed successfully!";
            return RedirectToPage("/Order/Status", new { customerOrderId = createdOrder.CustomerOrderId, subdomain = subdomain });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for restaurant {RestaurantId}", Restaurant.Id);
            ModelState.AddModelError(string.Empty, "An error occurred while creating your order. Please try again.");
            return Page();
        }
    }
}