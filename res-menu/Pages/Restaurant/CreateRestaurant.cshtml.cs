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
public class CreateRestaurantModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CreateRestaurantModel> _logger;

    public CreateRestaurantModel(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment,
        ILogger<CreateRestaurantModel> logger)
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

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during GET request");
            return NotFound();
        }

        var existingRestaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

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

        // Set OwnerId before model validation
        Restaurant.OwnerId = user.Id;

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid: {Errors}", 
                string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)));
            return Page();
        }

        // Check if subdomain is already taken
        var subdomainExists = await _context.Restaurants
            .AnyAsync(r => r.Subdomain.ToLower() == Restaurant.Subdomain.ToLower());

        if (subdomainExists)
        {
            _logger.LogWarning("Subdomain {Subdomain} is already taken", Restaurant.Subdomain);
            ModelState.AddModelError("Restaurant.Subdomain", "This subdomain is already taken.");
            return Page();
        }

        try
        {
            if (LogoFile != null)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "logos");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{LogoFile.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                _logger.LogInformation("Saving logo file to: {FilePath}", filePath);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(fileStream);
                }

                Restaurant.LogoUrl = $"/uploads/logos/{uniqueFileName}";
            }

            _logger.LogInformation("Adding restaurant to database: {RestaurantName}", Restaurant.Name);
            _context.Restaurants.Add(Restaurant);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Restaurant created successfully");

            return RedirectToPage("./ManageMenu");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant");
            ModelState.AddModelError(string.Empty, "An error occurred while creating the restaurant. Please try again.");
            return Page();
        }
    }
} 