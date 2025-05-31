using Microsoft.Extensions.Caching.Memory;

namespace res_menu.Services;

public interface ICacheService
{
    /// <summary>
    /// Get cached value by key
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>Cached value or default if not found</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Set cache value with expiration
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache expiration time</param>
    void Set<T>(string key, T value, TimeSpan expiration);

    /// <summary>
    /// Set cache value with absolute expiration
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="absoluteExpiration">Absolute expiration time</param>
    void Set<T>(string key, T value, DateTimeOffset absoluteExpiration);

    /// <summary>
    /// Remove value from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    void Remove(string key);

    /// <summary>
    /// Check if key exists in cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>True if key exists</returns>
    bool Exists(string key);

    /// <summary>
    /// Get or set cache value
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Factory function to create value if not cached</param>
    /// <param name="expiration">Cache expiration time</param>
    /// <returns>Cached or newly created value</returns>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public T? Get<T>(string key)
    {
        try
        {
            return _memoryCache.Get<T>(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return default;
        }
    }

    public void Set<T>(string key, T value, TimeSpan expiration)
    {
        try
        {
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = expiration,
                Priority = CacheItemPriority.Normal
            };

            _memoryCache.Set(key, value, options);
            _logger.LogDebug("Cache value set for key: {Key}, Expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public void Set<T>(string key, T value, DateTimeOffset absoluteExpiration)
    {
        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration,
                Priority = CacheItemPriority.Normal
            };

            _memoryCache.Set(key, value, options);
            _logger.LogDebug("Cache value set for key: {Key}, Absolute Expiration: {AbsoluteExpiration}", key, absoluteExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public void Remove(string key)
    {
        try
        {
            _memoryCache.Remove(key);
            _logger.LogDebug("Cache value removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }
    }

    public bool Exists(string key)
    {
        try
        {
            return _memoryCache.TryGetValue(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
            return false;
        }
    }    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out object? cachedObject) && cachedObject is T cachedValue)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            _logger.LogDebug("Cache miss for key: {Key}, executing factory", key);
            var value = await factory();
            Set(key, value, expiration);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
            
            // If cache operation fails, still try to execute factory
            return await factory();
        }
    }
}

public static class CacheKeys
{
    public const string RestaurantBySubdomain = "restaurant_subdomain_{0}";
    public const string RestaurantByOwnerId = "restaurant_owner_{0}";
    public const string MenuItemsByRestaurant = "menu_items_restaurant_{0}_available_{1}";
    public const string CategoriesByRestaurant = "categories_restaurant_{0}";
    public const string SubdomainAvailability = "subdomain_available_{0}";

    public static string RestaurantBySubdomainKey(string subdomain) => string.Format(RestaurantBySubdomain, subdomain.ToLower());
    public static string RestaurantByOwnerIdKey(string ownerId) => string.Format(RestaurantByOwnerId, ownerId);
    public static string MenuItemsByRestaurantKey(int restaurantId, bool includeUnavailable = true) => string.Format(MenuItemsByRestaurant, restaurantId, includeUnavailable);
    public static string CategoriesByRestaurantKey(int restaurantId) => string.Format(CategoriesByRestaurant, restaurantId);
    public static string SubdomainAvailabilityKey(string subdomain) => string.Format(SubdomainAvailability, subdomain.ToLower());
}
