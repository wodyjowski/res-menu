using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class ManageMenuModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ManageMenuModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IList<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public Models.Restaurant? Restaurant { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        Restaurant = await _context.Restaurants
            .FirstOrDefaultAsync(r => r.OwnerId == user.Id);

        if (Restaurant == null)
        {
            return RedirectToPage("./CreateRestaurant");
        }

        MenuItems = await _context.MenuItems
            .Where(m => m.RestaurantId == Restaurant.Id)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteMenuItemAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        var menuItem = await _context.MenuItems
            .Include(m => m.Restaurant)
            .FirstOrDefaultAsync(m => m.Id == id && m.Restaurant.OwnerId == user.Id);

        if (menuItem != null)
        {
            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
} 