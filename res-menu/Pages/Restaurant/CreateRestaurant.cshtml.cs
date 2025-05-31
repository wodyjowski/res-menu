using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Services;
using res_menu.Validation;
using System.ComponentModel.DataAnnotations;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class CreateRestaurantModel : PageModel
{
    private readonly IRestaurantService _restaurantService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<CreateRestaurantModel> _logger;

    public CreateRestaurantModel(
        IRestaurantService restaurantService,
        UserManager<IdentityUser> userManager,
        ILogger<CreateRestaurantModel> logger)
    {
        _restaurantService = restaurantService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public Models.Restaurant Restaurant { get; set; } = new();

    [BindProperty]
    [FileUploadValidation(MaxFileSizeBytes = 5 * 1024 * 1024, IsRequired = false)]
    [Display(Name = "Restaurant Logo")]
    public IFormFile? LogoFile { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during GET request");
            return NotFound();
        }

        var existingRestaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(user.Id);
        if (existingRestaurant != null)
        {
            _logger.LogInformation("User already has a restaurant, redirecting to ManageMenu");
            return RedirectToPage("./ManageMenu");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _logger.LogInformation("OnPostAsync started");
        
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during POST request");
            return NotFound();
        }

        // Set OwnerId before model validation - this must be done first
        Restaurant.OwnerId = user.Id;
        
        // Remove OwnerId from model state validation since we set it programmatically
        ModelState.Remove("Restaurant.OwnerId");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid: {Errors}", 
                string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)));
            return Page();
        }

        try
        {
            _logger.LogInformation("Creating restaurant: {RestaurantName}", Restaurant.Name);
            
            var result = await _restaurantService.CreateRestaurantAsync(Restaurant, LogoFile);
            
            if (!result.Success)
            {
                _logger.LogWarning("Restaurant creation failed: {Errors}", string.Join("; ", result.Errors));
                foreach (var error in result.Errors)
                {
                    if (error.Contains("subdomain", StringComparison.OrdinalIgnoreCase) || 
                        error.Contains("Subdomain", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("Restaurant.Subdomain", error);
                    }
                    else if (error.Contains("logo", StringComparison.OrdinalIgnoreCase) || 
                             error.Contains("Logo", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("LogoFile", error);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }
                return Page();
            }

            _logger.LogInformation("Restaurant created successfully with ID: {RestaurantId}", result.Data?.Id);
            return RedirectToPage("./ManageMenu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant: {RestaurantName}", Restaurant.Name);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while creating the restaurant. Please try again.");
            return Page();
        }
    }
} 