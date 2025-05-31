using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Models;
using res_menu.Services;

namespace res_menu.Pages;

[AllowAnonymous]
public class MenuModel : PageModel
{
    private readonly IRestaurantService _restaurantService;
    private readonly IMenuItemService _menuItemService;
    private readonly ILogger<MenuModel> _logger;

    public MenuModel(
        IRestaurantService restaurantService,
        IMenuItemService menuItemService,
        ILogger<MenuModel> logger)
    {
        _restaurantService = restaurantService;
        _menuItemService = menuItemService;
        _logger = logger;
    }

    public res_menu.Models.Restaurant? Restaurant { get; set; }
    public List<MenuItem> MenuItems { get; set; } = new();    public async Task<IActionResult> OnGetAsync(string? subdomain)
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

        Restaurant = await _restaurantService.GetRestaurantBySubdomainAsync(subdomain);
        if (Restaurant == null)
        {
            _logger.LogWarning("Restaurant not found for subdomain: {Subdomain}", subdomain);
            return NotFound($"Restaurant not found for subdomain: {subdomain}");
        }

        var allMenuItems = await _menuItemService.GetMenuItemsByRestaurantIdAsync(Restaurant.Id, includeUnavailable: false);
        MenuItems = allMenuItems
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToList();

        return Page();
    }
} 