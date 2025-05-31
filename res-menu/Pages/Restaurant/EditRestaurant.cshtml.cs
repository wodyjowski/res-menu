using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class EditRestaurantModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EditRestaurantModel> _logger;

    public EditRestaurantModel(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment,
        ILogger<EditRestaurantModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
    }

    [BindProperty]
    public Models.Restaurant Restaurant { get; set; } = new();

    [BindProperty]
    public IFormFile? LogoFile { get; set; }

    public string? CurrentLogoUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during GET request");
            return NotFound();
        }

        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

        if (restaurant == null)
        {
            _logger.LogInformation("User has no restaurant, redirecting to CreateRestaurant");
            return RedirectToPage("./CreateRestaurant");
        }

        Restaurant = restaurant;
        CurrentLogoUrl = restaurant.LogoUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _logger.LogInformation("EditRestaurant OnPostAsync started");
        
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during POST request");
            return NotFound();
        }

        var existingRestaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

        if (existingRestaurant == null)
        {
            _logger.LogWarning("Restaurant not found for user");
            return NotFound();
        }

        // Set OwnerId and Id from existing restaurant
        Restaurant.OwnerId = user.Id;
        Restaurant.Id = existingRestaurant.Id;

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid: {Errors}", 
                string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)));
            CurrentLogoUrl = existingRestaurant.LogoUrl;
            return Page();
        }

        // Check if subdomain is already taken by another restaurant
        var subdomainExists = await _context.Restaurants
            .AnyAsync(r => r.Subdomain.ToLower() == Restaurant.Subdomain.ToLower() && r.Id != Restaurant.Id);

        if (subdomainExists)
        {
            _logger.LogWarning("Subdomain {Subdomain} is already taken", Restaurant.Subdomain);
            ModelState.AddModelError("Restaurant.Subdomain", "This subdomain is already taken.");
            CurrentLogoUrl = existingRestaurant.LogoUrl;
            return Page();
        }

        try
        {
            // Handle logo upload
            if (LogoFile != null)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{LogoFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                _logger.LogInformation("Saving new logo file to: {FilePath}", filePath);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(fileStream);
                }

                // Delete old logo if it exists
                if (!string.IsNullOrEmpty(existingRestaurant.LogoUrl))
                {
                    var oldLogoPath = Path.Combine(_environment.WebRootPath, existingRestaurant.LogoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldLogoPath))
                    {
                        System.IO.File.Delete(oldLogoPath);
                        _logger.LogInformation("Deleted old logo: {OldLogoPath}", oldLogoPath);
                    }
                }

                Restaurant.LogoUrl = $"/uploads/logos/{uniqueFileName}";
            }
            else
            {
                // Keep existing logo if no new one uploaded
                Restaurant.LogoUrl = existingRestaurant.LogoUrl;
            }

            // Update the existing restaurant
            _context.Entry(existingRestaurant).CurrentValues.SetValues(Restaurant);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Restaurant updated successfully");

            TempData["SuccessMessage"] = "Restaurant profile updated successfully!";
            return RedirectToPage("./ManageMenu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant");
            ModelState.AddModelError(string.Empty, "An error occurred while updating the restaurant. Please try again.");
            CurrentLogoUrl = existingRestaurant.LogoUrl;
            return Page();
        }
    }
}
