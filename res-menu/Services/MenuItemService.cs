using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Services;

public class MenuItemService : IMenuItemService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileUploadService _fileUploadService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<MenuItemService> _logger;

    public MenuItemService(
        ApplicationDbContext context,
        IFileUploadService fileUploadService,
        ICacheService cacheService,
        ILogger<MenuItemService> logger)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _cacheService = cacheService;
        _logger = logger;
    }    public async Task<List<MenuItem>> GetMenuItemsByRestaurantIdAsync(int restaurantId, bool includeUnavailable = true)
    {
        try
        {
            var cacheKey = CacheKeys.MenuItemsByRestaurantKey(restaurantId, includeUnavailable);
            var cachedMenuItems = _cacheService.Get<List<MenuItem>>(cacheKey);
            
            if (cachedMenuItems != null)
            {
                _logger.LogDebug("Retrieved menu items from cache for restaurant: {RestaurantId}", restaurantId);
                return cachedMenuItems;
            }

            var query = _context.MenuItems.Where(m => m.RestaurantId == restaurantId);
            
            if (!includeUnavailable)
            {
                query = query.Where(m => m.IsAvailable);
            }

            var menuItems = await query
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .ToListAsync();

            // Cache for 10 minutes
            _cacheService.Set(cacheKey, menuItems, TimeSpan.FromMinutes(10));
            _logger.LogDebug("Cached menu items for restaurant: {RestaurantId}", restaurantId);

            return menuItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu items for restaurant: {RestaurantId}", restaurantId);
            return new List<MenuItem>();
        }
    }

    public async Task<MenuItem?> GetMenuItemByIdAsync(int id, string ownerId)
    {        try
        {
            return await _context.MenuItems
                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == id && m.Restaurant!.OwnerId == ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu item by ID: {MenuItemId}, Owner: {OwnerId}", id, ownerId);
            return null;
        }
    }

    public async Task<ServiceResult<MenuItem>> CreateMenuItemAsync(MenuItem menuItem, IFormFile? imageFile, string ownerId)
    {
        try
        {
            // Verify restaurant ownership
            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.Id == menuItem.RestaurantId && r.OwnerId == ownerId);

            if (restaurant == null)
            {
                return ServiceResult.ErrorResult<MenuItem>("Restaurant not found or you don't have permission to add items to it.");
            }

            string? imageUrl = null;

            // Handle image upload if provided
            if (imageFile != null)
            {
                _logger.LogInformation("Processing image upload for menu item: {MenuItemName}", menuItem.Name);
                
                var uploadResult = await _fileUploadService.UploadFileAsync(imageFile, UploadType.MenuItem);
                if (!uploadResult.Success)
                {
                    _logger.LogWarning("Image upload failed: {Errors}", string.Join("; ", uploadResult.Errors));
                    return ServiceResult.ErrorResult<MenuItem>(uploadResult.Errors.ToArray());
                }
                
                imageUrl = uploadResult.FileUrl;
                menuItem.ImageUrl = imageUrl;
            }            // Add menu item to database
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            
            // Invalidate related caches
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(menuItem.RestaurantId, true));
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(menuItem.RestaurantId, false));
            _cacheService.Remove(CacheKeys.CategoriesByRestaurantKey(menuItem.RestaurantId));
            
            _logger.LogInformation("Menu item created successfully with ID: {MenuItemId}", menuItem.Id);
            
            return ServiceResult.SuccessResult(menuItem, "Menu item created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating menu item: {MenuItemName}", menuItem.Name);
            
            // Clean up uploaded file if database operation failed
            if (!string.IsNullOrEmpty(menuItem.ImageUrl))
            {
                await _fileUploadService.DeleteFileAsync(menuItem.ImageUrl);
                _logger.LogInformation("Cleaned up uploaded image file after database error");
            }
            
            return ServiceResult.ErrorResult<MenuItem>("An error occurred while creating the menu item. Please try again.");
        }
    }

    public async Task<ServiceResult<MenuItem>> UpdateMenuItemAsync(MenuItem menuItem, IFormFile? imageFile, string ownerId)
    {
        try
        {            var existingMenuItem = await _context.MenuItems
                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == menuItem.Id && m.Restaurant!.OwnerId == ownerId);

            if (existingMenuItem == null)
            {
                return ServiceResult.ErrorResult<MenuItem>("Menu item not found or you don't have permission to edit it.");
            }

            string? oldImageUrl = existingMenuItem.ImageUrl;

            // Handle image upload if provided
            if (imageFile != null)
            {
                _logger.LogInformation("Processing image upload for menu item update: {MenuItemName}", menuItem.Name);
                
                var uploadResult = await _fileUploadService.UploadFileAsync(imageFile, UploadType.MenuItem);
                if (!uploadResult.Success)
                {
                    _logger.LogWarning("Image upload failed: {Errors}", string.Join("; ", uploadResult.Errors));
                    return ServiceResult.ErrorResult<MenuItem>(uploadResult.Errors.ToArray());
                }
                
                menuItem.ImageUrl = uploadResult.FileUrl;
            }
            else
            {
                // Keep existing image if no new file provided
                menuItem.ImageUrl = existingMenuItem.ImageUrl;
            }

            // Update menu item properties
            existingMenuItem.Name = menuItem.Name;
            existingMenuItem.Description = menuItem.Description;
            existingMenuItem.Price = menuItem.Price;
            existingMenuItem.Category = menuItem.Category;
            existingMenuItem.ImageUrl = menuItem.ImageUrl;
            existingMenuItem.IsAvailable = menuItem.IsAvailable;            await _context.SaveChangesAsync();

            // Invalidate related caches
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(existingMenuItem.RestaurantId, true));
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(existingMenuItem.RestaurantId, false));
            _cacheService.Remove(CacheKeys.CategoriesByRestaurantKey(existingMenuItem.RestaurantId));

            // Delete old image if a new one was uploaded
            if (imageFile != null && !string.IsNullOrEmpty(oldImageUrl) && oldImageUrl != menuItem.ImageUrl)
            {
                await _fileUploadService.DeleteFileAsync(oldImageUrl);
                _logger.LogInformation("Deleted old image file: {OldImageUrl}", oldImageUrl);
            }

            _logger.LogInformation("Menu item updated successfully: {MenuItemId}", menuItem.Id);
            
            return ServiceResult.SuccessResult(existingMenuItem, "Menu item updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu item: {MenuItemId}", menuItem.Id);
            
            // Clean up uploaded file if database operation failed
            if (imageFile != null && !string.IsNullOrEmpty(menuItem.ImageUrl))
            {
                await _fileUploadService.DeleteFileAsync(menuItem.ImageUrl);
                _logger.LogInformation("Cleaned up uploaded image file after database error");
            }
            
            return ServiceResult.ErrorResult<MenuItem>("An error occurred while updating the menu item. Please try again.");
        }
    }

    public async Task<ServiceResult> DeleteMenuItemAsync(int id, string ownerId)
    {
        try
        {            var menuItem = await _context.MenuItems
                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == id && m.Restaurant!.OwnerId == ownerId);

            if (menuItem == null)
            {
                return ServiceResult.ErrorResult("Menu item not found or you don't have permission to delete it.");
            }            string? imageUrl = menuItem.ImageUrl;
            int restaurantId = menuItem.RestaurantId;

            // Remove menu item from database
            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();

            // Invalidate related caches
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(restaurantId, true));
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(restaurantId, false));
            _cacheService.Remove(CacheKeys.CategoriesByRestaurantKey(restaurantId));

            // Delete associated image file
            if (!string.IsNullOrEmpty(imageUrl))
            {
                await _fileUploadService.DeleteFileAsync(imageUrl);
                _logger.LogInformation("Deleted image file: {ImageUrl}", imageUrl);
            }

            _logger.LogInformation("Menu item deleted successfully: {MenuItemId}", id);
            
            return ServiceResult.SuccessResult("Menu item deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu item: {MenuItemId}", id);
            return ServiceResult.ErrorResult("An error occurred while deleting the menu item. Please try again.");
        }
    }    public async Task<List<string>> GetCategoriesByRestaurantIdAsync(int restaurantId)
    {
        try
        {
            var cacheKey = CacheKeys.CategoriesByRestaurantKey(restaurantId);
            var cachedCategories = _cacheService.Get<List<string>>(cacheKey);
            
            if (cachedCategories != null)
            {
                _logger.LogDebug("Retrieved categories from cache for restaurant: {RestaurantId}", restaurantId);
                return cachedCategories;
            }

            var categories = await _context.MenuItems
                .Where(m => m.RestaurantId == restaurantId && !string.IsNullOrEmpty(m.Category))
                .Select(m => m.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Cache for 15 minutes
            _cacheService.Set(cacheKey, categories, TimeSpan.FromMinutes(15));
            _logger.LogDebug("Cached categories for restaurant: {RestaurantId}", restaurantId);

            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories for restaurant: {RestaurantId}", restaurantId);
            return new List<string>();
        }
    }

    public async Task<ServiceResult<bool>> ToggleAvailabilityAsync(int id, string ownerId)
    {
        try
        {
            var menuItem = await _context.MenuItems                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == id && m.Restaurant!.OwnerId == ownerId);

            if (menuItem == null)
            {
                return ServiceResult.ErrorResult<bool>("Menu item not found or you don't have permission to modify it.");
            }            menuItem.IsAvailable = !menuItem.IsAvailable;
            await _context.SaveChangesAsync();

            // Invalidate related caches (especially the available-only cache)
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(menuItem.RestaurantId, true));
            _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(menuItem.RestaurantId, false));

            _logger.LogInformation("Menu item availability toggled: {MenuItemId}, Available: {IsAvailable}", id, menuItem.IsAvailable);
            
            return ServiceResult.SuccessResult(menuItem.IsAvailable, $"Menu item {(menuItem.IsAvailable ? "enabled" : "disabled")} successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling menu item availability: {MenuItemId}", id);
            return ServiceResult.ErrorResult<bool>("An error occurred while updating the menu item. Please try again.");
        }
    }

    public async Task<ServiceResult> BulkUpdateAvailabilityAsync(List<int> itemIds, bool isAvailable, string ownerId)
    {
        try
        {            var menuItems = await _context.MenuItems
                .Include(m => m.Restaurant)
                .Where(m => itemIds.Contains(m.Id) && m.Restaurant!.OwnerId == ownerId)
                .ToListAsync();

            if (!menuItems.Any())
            {
                return ServiceResult.ErrorResult("No menu items found or you don't have permission to modify them.");
            }            foreach (var menuItem in menuItems)
            {
                menuItem.IsAvailable = isAvailable;
            }

            await _context.SaveChangesAsync();

            // Invalidate related caches for all affected restaurants
            var affectedRestaurantIds = menuItems.Select(m => m.RestaurantId).Distinct();
            foreach (var restaurantId in affectedRestaurantIds)
            {
                _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(restaurantId, true));
                _cacheService.Remove(CacheKeys.MenuItemsByRestaurantKey(restaurantId, false));
            }

            _logger.LogInformation("Bulk updated {Count} menu items availability to {IsAvailable}", menuItems.Count, isAvailable);
            
            return ServiceResult.SuccessResult($"Successfully updated {menuItems.Count} menu items.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating menu items availability");
            return ServiceResult.ErrorResult("An error occurred while updating the menu items. Please try again.");
        }
    }
}
