using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class EditMenuItemModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public EditMenuItemModel(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    [BindProperty]
    public MenuItem MenuItem { get; set; } = default!;

    [BindProperty]
    public IFormFile? ImageFile { get; set; }

    public List<string> ExistingCategories { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var menuItem = await _context.MenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == id && m.Restaurant.OwnerId == user.Id);

        if (menuItem == null)
        {
            return NotFound();
        }

        MenuItem = menuItem;

        // Get existing categories for the restaurant
        ExistingCategories = await _context.MenuItems
            .Where(m => m.RestaurantId == MenuItem.RestaurantId && !string.IsNullOrEmpty(m.Category))
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

        // Verify ownership
        var originalItem = await _context.MenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == MenuItem.Id && m.Restaurant.OwnerId == user.Id);

        if (originalItem == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Handle image upload
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

            // Delete old image if exists
            if (!string.IsNullOrEmpty(originalItem.ImageUrl))
            {
                var oldFilePath = Path.Combine(_environment.WebRootPath, 
                    originalItem.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            MenuItem.ImageUrl = $"/uploads/menu-items/{uniqueFileName}";
        }
        else
        {
            // Keep the existing image URL if no new image is uploaded
            MenuItem.ImageUrl = originalItem.ImageUrl;
        }

        // Update the original item with new values
        originalItem.Name = MenuItem.Name;
        originalItem.Description = MenuItem.Description;
        originalItem.Price = MenuItem.Price;
        originalItem.Category = MenuItem.Category;
        originalItem.IsAvailable = MenuItem.IsAvailable;
        originalItem.ImageUrl = MenuItem.ImageUrl;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await MenuItemExists(MenuItem.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return RedirectToPage("./ManageMenu");
    }

    private async Task<bool> MenuItemExists(int id)
    {
        return await _context.MenuItems.AnyAsync(e => e.Id == id);
    }
} 