using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Models;
using res_menu.Services;
using res_menu.Validation;
using System.ComponentModel.DataAnnotations;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class CreateMenuItemModel : PageModel
{
    private readonly IRestaurantService _restaurantService;
    private readonly IMenuItemService _menuItemService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<CreateMenuItemModel> _logger;

    public CreateMenuItemModel(
        IRestaurantService restaurantService,
        IMenuItemService menuItemService,
        UserManager<IdentityUser> userManager,
        ILogger<CreateMenuItemModel> logger)
    {
        _restaurantService = restaurantService;
        _menuItemService = menuItemService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public MenuItem MenuItem { get; set; } = new() { IsAvailable = true };

    [BindProperty]
    [FileUploadValidation(MaxFileSizeBytes = 10 * 1024 * 1024, IsRequired = false)]
    [Display(Name = "Item Image")]
    public IFormFile? ImageFile { get; set; }

    public List<string> ExistingCategories { get; set; } = new();    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var restaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(user.Id);
        if (restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }        // Get existing categories for the restaurant
        ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(restaurant.Id);

        return Page();
    }    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var restaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(user.Id);
        if (restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }        // Get existing categories for validation display
        ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(restaurant.Id);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        MenuItem.RestaurantId = restaurant.Id;        try
        {
            var result = await _menuItemService.CreateMenuItemAsync(MenuItem, ImageFile, user.Id);
            
            if (result.Success)
            {
                _logger.LogInformation("Menu item created successfully: {MenuItemName} for restaurant {RestaurantId}", 
                    MenuItem.Name, restaurant.Id);
                return RedirectToPage("./ManageMenu");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                return Page();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item");
            ModelState.AddModelError("", "An error occurred while creating the menu item. Please try again.");
            return Page();
        }
    }
} 