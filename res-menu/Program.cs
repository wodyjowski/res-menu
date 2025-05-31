using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Extensions;
using Npgsql;
using System.Net.Sockets;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Configure culture for PLN currency
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "pl-PL" };
    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Configure Kestrel to use HTTPS
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8888); // HTTP - use port 8888 instead of 80
    
    // Only configure HTTPS if we're in development or have valid certificates
    if (builder.Environment.IsDevelopment())
    {
        serverOptions.ListenAnyIP(443, listenOptions =>
        {
            listenOptions.UseHttps(); // Uses development certificate
        });
    }
    else
    {
        // In production, check for Let's Encrypt certificates first
        var certPath = "/etc/letsencrypt/live/res-menu.duckdns.org/fullchain.pem";
        var keyPath = "/etc/letsencrypt/live/res-menu.duckdns.org/privkey.pem";
        
        if (File.Exists(certPath) && File.Exists(keyPath))
        {
            Console.WriteLine($"SSL certificate files found successfully. Certificate: {certPath}, Key: {keyPath}");
            
            serverOptions.ListenAnyIP(443, listenOptions =>
            {
                listenOptions.UseHttps(certPath, keyPath);
            });
        }
        else
        {
            if (!File.Exists(certPath))
            {
                Console.WriteLine($"SSL certificate file not found at path: {certPath}");
            }
            
            if (!File.Exists(keyPath))
            {
                Console.WriteLine($"SSL private key file not found at path: {keyPath}");
            }
            
            Console.WriteLine("SSL certificates not found. HTTPS endpoint will not be available until certificates are configured.");
        }
    }
});

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    Environment.GetEnvironmentVariable("DATABASE_URL") ?? 
    throw new InvalidOperationException("Connection string not found. Please set either DefaultConnection in configuration or DATABASE_URL environment variable.");

// Log the connection string (without credentials) at startup
var sanitizedConnectionString = connectionString.Contains("@") 
    ? new Uri(connectionString).Host 
    : connectionString.Replace(";Password=", ";Password=***").Replace(";User Id=", ";User Id=***").Replace(";Username=", ";Username=***");
Console.WriteLine($"Using database connection: {sanitizedConnectionString}");

// If the connection string is a Heroku-style URL (postgres://user:pass@host:port/db)
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

// Only change postgres to localhost in development AND when not running in Docker
if (builder.Environment.IsDevelopment() && 
    !Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true && 
    connectionString.Contains("Host=postgres"))
{
    connectionString = connectionString.Replace("Host=postgres", "Host=localhost");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorCodesToAdd: null);
    });
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure Data Protection
if (builder.Environment.IsProduction())
{
    // In production, store keys in the database for persistence across container restarts
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<ApplicationDbContext>()
        .SetApplicationName("res-menu");
}
else
{
    // In development, you can use the default file system or configure as needed
    builder.Services.AddDataProtection()
        .SetApplicationName("res-menu");
}

// Configure Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 1;
    options.Password.RequiredUniqueChars = 0;
    
    // Use username instead of email
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.Name = "res_menu_auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Register dynamic port service
builder.Services.AddScoped<ResMenu.Services.IDynamicPortService, ResMenu.Services.DynamicPortService>();

// Register application services including IOrderService
builder.Services.AddApplicationServices();

var app = builder.Build();

// Add error handling middleware at the beginning of the pipeline
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation(
            "Starting request processing - Method: {Method}, Path: {Path}, Host: {Host}",
            context.Request.Method,
            context.Request.Path,
            context.Request.Host.Value
        );
        
        await next();
        
        logger.LogInformation(
            "Request completed - Status Code: {StatusCode}",
            context.Response.StatusCode
        );
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Error processing request - Method: {Method}, Path: {Path}, Host: {Host}, Error: {Error}",
            context.Request.Method,
            context.Request.Path,
            context.Request.Host.Value,
            ex.Message
        );
        throw;
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Add localization middleware at the beginning of the pipeline
app.UseRequestLocalization();

// Add database error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (
        ex is PostgresException ||
        ex is NpgsqlException ||
        ex is SocketException ||
        (ex.InnerException is PostgresException) ||
        (ex.InnerException is NpgsqlException) ||
        (ex.InnerException is SocketException))
    {
        // Log the error with connection details (but not credentials)
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
        var dbConnectionString = dbContext.Database.GetConnectionString() ?? "";
        var sanitizedConnectionString = SanitizeConnectionString(dbConnectionString);
        
        logger.LogError(ex, "Database connection error occurred. Connection details: {ConnectionDetails}", sanitizedConnectionString);

        // Redirect to error page with database error flag
        context.Response.Redirect("/Error?errorType=database");
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Add port detection middleware to store current port in cookie
app.Use(async (context, next) =>
{
    var portService = context.RequestServices.GetRequiredService<ResMenu.Services.IDynamicPortService>();
    var currentPort = context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80);
    
    // Store port in cookie if it's different from the stored one
    var storedPort = portService.GetPortFromCookie(context);
    if (!storedPort.HasValue || storedPort.Value != currentPort)
    {
        portService.StorePortInCookie(context, currentPort);
    }
    
    await next();
});

// Add subdomain routing middleware BEFORE UseRouting and authentication
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var host = context.Request.Host.Host.ToLower();
    var isLocalhost = host == "localhost" || host.StartsWith("127.") || host.StartsWith("192.168.") || host.StartsWith("10.");
    
    logger.LogInformation(
        "Subdomain Middleware - Incoming Request - Host: {Host}, Path: {Path}, IsLocalhost: {IsLocalhost}",
        host,
        context.Request.Path,
        isLocalhost
    );
    
    string? detectedSubdomain = null;

    // Check if we're on a subdomain of res-menu.duckdns.org
    if (!isLocalhost && host.EndsWith("res-menu.duckdns.org") && host != "res-menu.duckdns.org")
    {
        var parts = host.Split('.');
        if (parts.Length > 2 && parts[0] != "www") // e.g. zxc.res-menu.duckdns.org
        {
            detectedSubdomain = parts[0];
            logger.LogInformation(
                "Subdomain Middleware - Subdomain detected from host: {Subdomain}, Original Path: {OriginalPath}",
                detectedSubdomain,
                context.Request.Path
            );
        }
    }
    
    if (!string.IsNullOrEmpty(detectedSubdomain))
    {
        // If a subdomain is detected via hostname, prioritize it and rewrite to /Menu
        // This handles direct navigation to subdomain.res-menu.duckdns.org/any/path
        // We are assuming that any path on a subdomain should show the menu.
        // If there are other specific paths on subdomains (e.g., /api/...) they would need special handling here.

        logger.LogInformation("Changing request path to include subdomain: {Subdomain}", detectedSubdomain);
        
        context.Request.Path = $"/Menu/{detectedSubdomain}"; // Force path to /Menu/subdomain
        context.Items["Subdomain"] = detectedSubdomain; // Set for MenuModel
        
        // Also add it as a query parameter, as MenuModel also checks this.
        // This makes the MenuModel's logic more robust if HttpContext.Items isn't read as expected
        // or if the route parameter isn't picked up as expected.
        context.Request.QueryString = QueryString.Create("subdomain", detectedSubdomain);

        logger.LogInformation(
            "Subdomain Middleware - Request rewritten for subdomain. New Path: {NewPath}, Subdomain Item: {SubdomainItem}, QueryString: {QueryString}",
            context.Request.Path,
            context.Items["Subdomain"],
            context.Request.QueryString.ToString()
        );
    }
    else if (isLocalhost && context.Request.Path.StartsWithSegments("/Menu", StringComparison.OrdinalIgnoreCase) && context.Request.Query.ContainsKey("subdomain"))
    {
        // For localhost, if it's /Menu?subdomain=xxx, ensure HttpContext.Items["Subdomain"] is also set
        // This helps MenuModel.cs if it prioritizes HttpContext.Items
        var SParam = context.Request.Query["subdomain"].FirstOrDefault();
        if(!string.IsNullOrEmpty(SParam))
        {
            context.Items["Subdomain"] = SParam;
             logger.LogInformation(
                "Subdomain Middleware - Localhost /Menu?subdomain=... detected. Set HttpContext.Items[\\\"Subdomain\\\"] = {Subdomain}", SParam);
        }
    }
    
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure endpoints with explicit authorization
app.MapRazorPages();

// Initialize/migrate database on startup
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Create database if it doesn't exist
    if (!await dbContext.Database.CanConnectAsync())
    {
        logger.LogInformation("Database does not exist. Creating database...");
        await dbContext.Database.EnsureCreatedAsync();
        logger.LogInformation("Database created successfully.");
    }    // Check for pending migrations
    if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
    {
        logger.LogInformation("Applying pending migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");
    }
    
    // Log successful connection
    var dbConnectionString = dbContext.Database.GetConnectionString() ?? "";
    var sanitizedDbConnectionString = SanitizeConnectionString(dbConnectionString);
    logger.LogInformation("Successfully connected to database. Connection details: {ConnectionDetails}", sanitizedDbConnectionString);
}
catch (Exception ex) when (
    ex is PostgresException ||
    ex is NpgsqlException ||
    ex is SocketException ||
    (ex.InnerException is PostgresException) ||
    (ex.InnerException is NpgsqlException) ||
    (ex.InnerException is SocketException))
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Unable to initialize/migrate database");
    
    // Try to create the database if it doesn't exist
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Create the database
        using var conn = new NpgsqlConnection(connectionString.Replace("Database=res_menu;", "Database=postgres;"));
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE DATABASE res_menu;";
        await cmd.ExecuteNonQueryAsync();
        
        logger.LogInformation("Database created successfully. Applying migrations...");
        
        // Apply migrations
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception createEx)
    {
        logger.LogError(createEx, "Failed to create database and apply migrations");
    }
}

app.Run();

// Helper method to remove sensitive information from connection string
static string SanitizeConnectionString(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    return $"Host={builder.Host};Database={builder.Database};Port={builder.Port}";
}