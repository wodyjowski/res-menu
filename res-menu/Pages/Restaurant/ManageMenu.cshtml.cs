using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Models;
using res_menu.Services;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class ManageMenuModel : PageModel
{
    private readonly IRestaurantService _restaurantService;
    private readonly IMenuItemService _menuItemService;
    private readonly UserManager<IdentityUser> _userManager;

    public ManageMenuModel(
        IRestaurantService restaurantService, 
        IMenuItemService menuItemService,
        UserManager<IdentityUser> userManager)
    {
        _restaurantService = restaurantService;
        _menuItemService = menuItemService;
        _userManager = userManager;
    }

    public IList<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public Models.Restaurant? Restaurant { get; set; }    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        Restaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(user.Id);
        if (Restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }

        MenuItems = await _menuItemService.GetMenuItemsByRestaurantIdAsync(Restaurant.Id);

        return Page();
    }public async Task<IActionResult> OnPostDeleteMenuItemAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        var result = await _menuItemService.DeleteMenuItemAsync(id, user.Id);
        if (!result.Success)
        {
            // Optionally add error handling/messaging here
        }

        return RedirectToPage();
    }
} 