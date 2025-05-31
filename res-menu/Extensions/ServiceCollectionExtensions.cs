using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using res_menu.Data;
using res_menu.Services;

namespace res_menu.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, string connectionString)
    {        // Handle Heroku-style URL format
        if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
        {
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':');
            connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = userInfo[0],
                Password = userInfo[1],
                SslMode = Npgsql.SslMode.Prefer
            }.ToString();
        }

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(3),
                    errorCodesToAdd: null);
            });
        });

        services.AddDatabaseDeveloperPageExceptionFilter();
        
        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<IdentityUser, IdentityRole>(options => 
        {
            // Authentication settings
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;
            
            // Password settings
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 1;
            options.Password.RequiredUniqueChars = 0;
            
            // User settings
            options.User.RequireUniqueEmail = false;
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options => 
        {
            options.LoginPath = "/Identity/Account/Login";
            options.LogoutPath = "/Identity/Account/Logout";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            options.Cookie.Name = "res_menu_auth";
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddRazorPages(options => 
        {
            // Configure public pages
            options.Conventions.AllowAnonymousToPage("/Menu");
            options.Conventions.AllowAnonymousToPage("/Error");
            
            // Configure protected pages
            options.Conventions.AuthorizePage("/Restaurant/ManageMenu");
            options.Conventions.AuthorizePage("/Restaurant/CreateMenuItem");
            options.Conventions.AuthorizePage("/Restaurant/EditMenuItem");
            options.Conventions.AuthorizePage("/Restaurant/CreateRestaurant");
        });        // Register application services
        services.AddMemoryCache();
        services.AddScoped<IFileUploadService, FileUploadService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IMenuItemService, MenuItemService>();
        services.AddScoped<IErrorHandlingService, ErrorHandlingService>();
        services.AddScoped<ICacheService, CacheService>();

        return services;
    }
}
