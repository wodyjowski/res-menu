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
public class EditMenuItemModel : PageModel
{
    private readonly IRestaurantService _restaurantService;
    private readonly IMenuItemService _menuItemService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<EditMenuItemModel> _logger;

    public EditMenuItemModel(
        IRestaurantService restaurantService,
        IMenuItemService menuItemService,
        UserManager<IdentityUser> userManager,
        ILogger<EditMenuItemModel> logger)
    {
        _restaurantService = restaurantService;
        _menuItemService = menuItemService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public MenuItem MenuItem { get; set; } = default!;

    [BindProperty]
    [FileUploadValidation(MaxFileSizeBytes = 10 * 1024 * 1024, IsRequired = false)]
    [Display(Name = "Item Image")]
    public IFormFile? ImageFile { get; set; }

    public List<string> ExistingCategories { get; set; } = new();    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var menuItem = await _menuItemService.GetMenuItemByIdAsync(id, user.Id);
        if (menuItem == null)
        {
            return NotFound();
        }

        MenuItem = menuItem;        // Get existing categories for the restaurant
        ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(MenuItem.RestaurantId);

        return Page();
    }    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }        if (!ModelState.IsValid)
        {
            // Re-populate categories for display
            ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(MenuItem.RestaurantId);
            return Page();
        }

        try
        {
            var result = await _menuItemService.UpdateMenuItemAsync(MenuItem, ImageFile, user.Id);
            
            if (result.Success)
            {
                _logger.LogInformation("Menu item updated successfully: {MenuItemName} (ID: {MenuItemId})", 
                    MenuItem.Name, MenuItem.Id);
                return RedirectToPage("./ManageMenu");
            }
            else
            {                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                
                // Re-populate categories for display
                ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(MenuItem.RestaurantId);
                return Page();
            }
        }
        catch (Exception ex)
        {            _logger.LogError(ex, "Error updating menu item");
            ModelState.AddModelError("", "An error occurred while updating the menu item. Please try again.");
            
            // Re-populate categories for display
            ExistingCategories = await _menuItemService.GetCategoriesByRestaurantIdAsync(MenuItem.RestaurantId);
            return Page();
        }
    }
} 