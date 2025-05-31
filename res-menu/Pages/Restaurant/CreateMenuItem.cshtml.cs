using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class CreateMenuItemModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public CreateMenuItemModel(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    [BindProperty]
    public MenuItem MenuItem { get; set; } = new() { IsAvailable = true };

    [BindProperty]
    public IFormFile? ImageFile { get; set; }

    public List<string> ExistingCategories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

        if (restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }

        // Get existing categories for the restaurant
        ExistingCategories = await _context.MenuItems
            .Where(m => m.RestaurantId == restaurant.Id && !string.IsNullOrEmpty(m.Category))
            .Select(m => m.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

        if (restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }        if (!ModelState.IsValid)
        {
            // Repopulate ExistingCategories when validation fails
            ExistingCategories = await _context.MenuItems
                .Where(m => m.RestaurantId == restaurant.Id && !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            
            return Page();
        }

        MenuItem.RestaurantId = restaurant.Id;

        if (ImageFile != null)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "menu-items");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{ImageFile.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await ImageFile.CopyToAsync(fileStream);
            }

            MenuItem.ImageUrl = $"/uploads/menu-items/{uniqueFileName}";
        }

        _context.MenuItems.Add(MenuItem);
        await _context.SaveChangesAsync();

        return RedirectToPage("./ManageMenu");
    }
} 