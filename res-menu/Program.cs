using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using res_menu.Data;
using res_menu.Areas.Identity.Data;
using Microsoft.AspNetCore.Http.Extensions;
using Npgsql;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

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

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 1;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
app.UseRouting();

// Add subdomain routing middleware
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.ToLower();
    var isLocalhost = host == "localhost" || host.StartsWith("127.") || host.StartsWith("192.168.");
    
    if (!isLocalhost && host.Count(c => c == '.') > 1)
    {
        var subdomain = host.Split('.')[0];
        if (!string.IsNullOrEmpty(subdomain))
        {
            var originalPath = context.Request.Path;
            context.Request.Path = "/Menu";
            context.Request.QueryString = context.Request.QueryString.Add("subdomain", subdomain);
        }
    }
    
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

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
    }

    // Check for pending migrations
    if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
    {
        logger.LogInformation("Applying pending migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");
    }

    // Log successful connection
    var dbConnectionString = dbContext.Database.GetConnectionString() ?? "";
    var sanitizedConnectionString = SanitizeConnectionString(dbConnectionString);
    logger.LogInformation("Successfully connected to database. Connection details: {ConnectionDetails}", sanitizedConnectionString);
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