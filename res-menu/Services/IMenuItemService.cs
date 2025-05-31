using res_menu.Models;

namespace res_menu.Services;

public interface IMenuItemService
{
    /// <summary>
    /// Get all menu items for a restaurant
    /// </summary>
    /// <param name="restaurantId">Restaurant ID</param>
    /// <param name="includeUnavailable">Include unavailable items</param>
    /// <returns>List of menu items</returns>
    Task<List<MenuItem>> GetMenuItemsByRestaurantIdAsync(int restaurantId, bool includeUnavailable = true);

    /// <summary>
    /// Get menu item by ID
    /// </summary>
    /// <param name="id">Menu item ID</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Menu item if found and authorized</returns>
    Task<MenuItem?> GetMenuItemByIdAsync(int id, string ownerId);

    /// <summary>
    /// Create a new menu item
    /// </summary>
    /// <param name="menuItem">Menu item data</param>
    /// <param name="imageFile">Optional image file</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Created menu item</returns>
    Task<ServiceResult<MenuItem>> CreateMenuItemAsync(MenuItem menuItem, IFormFile? imageFile, string ownerId);

    /// <summary>
    /// Update existing menu item
    /// </summary>
    /// <param name="menuItem">Menu item data</param>
    /// <param name="imageFile">Optional image file</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Updated menu item</returns>
    Task<ServiceResult<MenuItem>> UpdateMenuItemAsync(MenuItem menuItem, IFormFile? imageFile, string ownerId);

    /// <summary>
    /// Delete menu item
    /// </summary>
    /// <param name="id">Menu item ID</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Success result</returns>
    Task<ServiceResult> DeleteMenuItemAsync(int id, string ownerId);

    /// <summary>
    /// Get existing categories for a restaurant
    /// </summary>
    /// <param name="restaurantId">Restaurant ID</param>
    /// <returns>List of unique categories</returns>
    Task<List<string>> GetCategoriesByRestaurantIdAsync(int restaurantId);

    /// <summary>
    /// Toggle menu item availability
    /// </summary>
    /// <param name="id">Menu item ID</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Updated availability status</returns>
    Task<ServiceResult<bool>> ToggleAvailabilityAsync(int id, string ownerId);

    /// <summary>
    /// Bulk update menu item availability
    /// </summary>
    /// <param name="itemIds">List of menu item IDs</param>
    /// <param name="isAvailable">Availability status</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Success result</returns>
    Task<ServiceResult> BulkUpdateAvailabilityAsync(List<int> itemIds, bool isAvailable, string ownerId);
}
