using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Models;

namespace res_menu.Services;

public class RestaurantService : IRestaurantService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileUploadService _fileUploadService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RestaurantService> _logger;

    public RestaurantService(
        ApplicationDbContext context,
        IFileUploadService fileUploadService,
        ICacheService cacheService,
        ILogger<RestaurantService> logger)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Restaurant?> GetRestaurantByOwnerIdAsync(string ownerId)
    {
        try
        {
            return await _context.Restaurants
                .Include(r => r.MenuItems)
                .FirstOrDefaultAsync(r => r.OwnerId == ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant by owner ID: {OwnerId}", ownerId);
            return null;
        }
    }    public async Task<Restaurant?> GetRestaurantBySubdomainAsync(string subdomain)
    {
        try
        {
            var cacheKey = CacheKeys.RestaurantBySubdomainKey(subdomain);
            
            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                return await _context.Restaurants
                    .Include(r => r.MenuItems.Where(m => m.IsAvailable))
                    .FirstOrDefaultAsync(r => r.Subdomain.ToLower() == subdomain.ToLower());
            }, TimeSpan.FromMinutes(15)); // Cache for 15 minutes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting restaurant by subdomain: {Subdomain}", subdomain);
            return null;
        }
    }

    public async Task<ServiceResult<Restaurant>> CreateRestaurantAsync(Restaurant restaurant, IFormFile? logoFile = null)
    {
        try
        {
            // Check if subdomain is already taken
            if (!await IsSubdomainAvailableAsync(restaurant.Subdomain))
            {
                return ServiceResult.ErrorResult<Restaurant>("This subdomain is already taken.");
            }

            string? logoUrl = null;

            // Handle logo upload if provided
            if (logoFile != null)
            {
                _logger.LogInformation("Processing logo upload for restaurant: {RestaurantName}", restaurant.Name);
                
                var uploadResult = await _fileUploadService.UploadFileAsync(logoFile, UploadType.Logo);
                if (!uploadResult.Success)
                {
                    _logger.LogWarning("Logo upload failed: {Errors}", string.Join("; ", uploadResult.Errors));
                    return ServiceResult.ErrorResult<Restaurant>(uploadResult.Errors.ToArray());
                }
                
                logoUrl = uploadResult.FileUrl;
                restaurant.LogoUrl = logoUrl;
            }            // Add restaurant to database
            _context.Restaurants.Add(restaurant);
            await _context.SaveChangesAsync();
            
            // Invalidate related caches
            _cacheService.Remove(CacheKeys.RestaurantBySubdomainKey(restaurant.Subdomain));
            _cacheService.Remove(CacheKeys.RestaurantByOwnerIdKey(restaurant.OwnerId));
            _cacheService.Remove(CacheKeys.SubdomainAvailabilityKey(restaurant.Subdomain));
            
            _logger.LogInformation("Restaurant created successfully with ID: {RestaurantId}", restaurant.Id);
            
            return ServiceResult.SuccessResult(restaurant, "Restaurant created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restaurant: {RestaurantName}", restaurant.Name);
            
            // Clean up uploaded file if database operation failed
            if (!string.IsNullOrEmpty(restaurant.LogoUrl))
            {
                await _fileUploadService.DeleteFileAsync(restaurant.LogoUrl);
                _logger.LogInformation("Cleaned up uploaded logo file after database error");
            }
            
            return ServiceResult.ErrorResult<Restaurant>("An error occurred while creating the restaurant. Please try again.");
        }
    }

    public async Task<ServiceResult<Restaurant>> UpdateRestaurantAsync(Restaurant restaurant, IFormFile? logoFile = null)
    {
        try
        {
            var existingRestaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.Id == restaurant.Id);

            if (existingRestaurant == null)
            {
                return ServiceResult.ErrorResult<Restaurant>("Restaurant not found.");
            }

            // Check if subdomain is available (excluding current restaurant)
            if (!await IsSubdomainAvailableAsync(restaurant.Subdomain, restaurant.Id))
            {
                return ServiceResult.ErrorResult<Restaurant>("This subdomain is already taken.");
            }

            string? oldLogoUrl = existingRestaurant.LogoUrl;

            // Handle logo upload if provided
            if (logoFile != null)
            {
                _logger.LogInformation("Processing logo upload for restaurant update: {RestaurantName}", restaurant.Name);
                
                var uploadResult = await _fileUploadService.UploadFileAsync(logoFile, UploadType.Logo);
                if (!uploadResult.Success)
                {
                    _logger.LogWarning("Logo upload failed: {Errors}", string.Join("; ", uploadResult.Errors));
                    return ServiceResult.ErrorResult<Restaurant>(uploadResult.Errors.ToArray());
                }
                
                restaurant.LogoUrl = uploadResult.FileUrl;
            }
            else
            {
                // Keep existing logo if no new file provided
                restaurant.LogoUrl = existingRestaurant.LogoUrl;
            }

            // Update restaurant properties
            existingRestaurant.Name = restaurant.Name;
            existingRestaurant.Subdomain = restaurant.Subdomain;
            existingRestaurant.Description = restaurant.Description;
            existingRestaurant.LogoUrl = restaurant.LogoUrl;

            await _context.SaveChangesAsync();

            // Delete old logo if a new one was uploaded
            if (logoFile != null && !string.IsNullOrEmpty(oldLogoUrl) && oldLogoUrl != restaurant.LogoUrl)
            {
                await _fileUploadService.DeleteFileAsync(oldLogoUrl);
                _logger.LogInformation("Deleted old logo file: {OldLogoUrl}", oldLogoUrl);
            }

            _logger.LogInformation("Restaurant updated successfully: {RestaurantId}", restaurant.Id);
            
            return ServiceResult.SuccessResult(existingRestaurant, "Restaurant updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating restaurant: {RestaurantId}", restaurant.Id);
            
            // Clean up uploaded file if database operation failed
            if (logoFile != null && !string.IsNullOrEmpty(restaurant.LogoUrl))
            {
                await _fileUploadService.DeleteFileAsync(restaurant.LogoUrl);
                _logger.LogInformation("Cleaned up uploaded logo file after database error");
            }
            
            return ServiceResult.ErrorResult<Restaurant>("An error occurred while updating the restaurant. Please try again.");
        }
    }

    public async Task<bool> IsSubdomainAvailableAsync(string subdomain, int? excludeRestaurantId = null)
    {
        try
        {
            var query = _context.Restaurants.Where(r => r.Subdomain.ToLower() == subdomain.ToLower());
            
            if (excludeRestaurantId.HasValue)
            {
                query = query.Where(r => r.Id != excludeRestaurantId.Value);
            }

            return !await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking subdomain availability: {Subdomain}", subdomain);
            return false;
        }
    }

    public async Task<ServiceResult> DeleteRestaurantAsync(int restaurantId, string ownerId)
    {
        try
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.MenuItems)
                .FirstOrDefaultAsync(r => r.Id == restaurantId && r.OwnerId == ownerId);

            if (restaurant == null)
            {
                return ServiceResult.ErrorResult("Restaurant not found or you don't have permission to delete it.");
            }

            // Delete associated files
            var filesToDelete = new List<string>();
            
            if (!string.IsNullOrEmpty(restaurant.LogoUrl))
            {
                filesToDelete.Add(restaurant.LogoUrl);
            }

            foreach (var menuItem in restaurant.MenuItems)
            {
                if (!string.IsNullOrEmpty(menuItem.ImageUrl))
                {
                    filesToDelete.Add(menuItem.ImageUrl);
                }
            }

            // Remove restaurant from database (cascade delete will handle menu items)
            _context.Restaurants.Remove(restaurant);
            await _context.SaveChangesAsync();

            // Delete files after successful database operation
            foreach (var fileUrl in filesToDelete)
            {
                await _fileUploadService.DeleteFileAsync(fileUrl);
            }

            _logger.LogInformation("Restaurant deleted successfully: {RestaurantId}", restaurantId);
            
            return ServiceResult.SuccessResult("Restaurant deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting restaurant: {RestaurantId}", restaurantId);
            return ServiceResult.ErrorResult("An error occurred while deleting the restaurant. Please try again.");
        }
    }
}
