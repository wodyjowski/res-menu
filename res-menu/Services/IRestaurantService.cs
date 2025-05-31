using res_menu.Models;

namespace res_menu.Services;

public interface IRestaurantService
{
    /// <summary>
    /// Get restaurant by owner ID
    /// </summary>
    /// <param name="ownerId">The owner's user ID</param>
    /// <returns>Restaurant if found, null otherwise</returns>
    Task<Restaurant?> GetRestaurantByOwnerIdAsync(string ownerId);

    /// <summary>
    /// Get restaurant by subdomain
    /// </summary>
    /// <param name="subdomain">The restaurant's subdomain</param>
    /// <returns>Restaurant if found, null otherwise</returns>
    Task<Restaurant?> GetRestaurantBySubdomainAsync(string subdomain);

    /// <summary>
    /// Create a new restaurant
    /// </summary>
    /// <param name="restaurant">Restaurant data</param>
    /// <param name="logoFile">Optional logo file</param>
    /// <returns>Created restaurant</returns>
    Task<ServiceResult<Restaurant>> CreateRestaurantAsync(Restaurant restaurant, IFormFile? logoFile = null);

    /// <summary>
    /// Update restaurant information
    /// </summary>
    /// <param name="restaurant">Restaurant data</param>
    /// <param name="logoFile">Optional logo file</param>
    /// <returns>Updated restaurant</returns>
    Task<ServiceResult<Restaurant>> UpdateRestaurantAsync(Restaurant restaurant, IFormFile? logoFile = null);

    /// <summary>
    /// Check if subdomain is available
    /// </summary>
    /// <param name="subdomain">Subdomain to check</param>
    /// <param name="excludeRestaurantId">Restaurant ID to exclude from check (for updates)</param>
    /// <returns>True if available, false otherwise</returns>
    Task<bool> IsSubdomainAvailableAsync(string subdomain, int? excludeRestaurantId = null);

    /// <summary>
    /// Delete restaurant and all associated data
    /// </summary>
    /// <param name="restaurantId">Restaurant ID</param>
    /// <param name="ownerId">Owner ID for authorization</param>
    /// <returns>Success result</returns>
    Task<ServiceResult> DeleteRestaurantAsync(int restaurantId, string ownerId);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }
}

public class ServiceResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }

    public static ServiceResult SuccessResult(string? message = null)
    {
        return new ServiceResult { Success = true, Message = message };
    }

    public static ServiceResult<T> SuccessResult<T>(T data, string? message = null)
    {
        return new ServiceResult<T> { Success = true, Data = data, Message = message };
    }

    public static ServiceResult ErrorResult(params string[] errors)
    {
        return new ServiceResult { Success = false, Errors = errors.ToList() };
    }

    public static ServiceResult<T> ErrorResult<T>(params string[] errors)
    {
        return new ServiceResult<T> { Success = false, Errors = errors.ToList() };
    }
}
