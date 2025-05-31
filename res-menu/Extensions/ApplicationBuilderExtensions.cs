using Microsoft.EntityFrameworkCore;
using Npgsql;
using res_menu.Data;
using System.Net.Sockets;

namespace res_menu.Extensions;

public static class ApplicationBuilderExtensions
{    public static IApplicationBuilder UseCustomExceptionHandling(this IApplicationBuilder app)
    {
        app.UseMiddleware<res_menu.Middleware.GlobalExceptionHandlingMiddleware>();
        return app;
    }    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation(
                "Processing request - Method: {Method}, Path: {Path}, Host: {Host}",
                context.Request.Method,
                context.Request.Path,
                context.Request.Host.Value
            );
            
            await next();
            
            logger.LogInformation(
                "Request completed - Status Code: {StatusCode}",
                context.Response.StatusCode
            );
        });

        return app;
    }

    public static IApplicationBuilder UseSubdomainRouting(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var host = context.Request.Host.Host.ToLower();
            var isLocalhost = host == "localhost" || host.StartsWith("127.") || host.StartsWith("192.168.");
            
            logger.LogInformation(
                "Request received - Host: {Host}, Path: {Path}, IsLocalhost: {IsLocalhost}",
                host,
                context.Request.Path,
                isLocalhost
            );
            
            // Check if we're on a subdomain of res-menu.duckdns.org
            if (!isLocalhost && host.EndsWith("res-menu.duckdns.org") && host != "res-menu.duckdns.org")
            {
                var subdomain = host.Split('.')[0];
                logger.LogInformation(
                    "Subdomain detected: {Subdomain}, Original Path: {OriginalPath}",
                    subdomain,
                    context.Request.Path
                );
                
                if (!string.IsNullOrEmpty(subdomain))
                {
                    context.Request.Path = "/Menu";
                    context.Request.QueryString = context.Request.QueryString.Add("subdomain", subdomain);
                    logger.LogInformation(
                        "Request rewritten - New Path: {NewPath}, QueryString: {QueryString}",
                        context.Request.Path,
                        context.Request.QueryString
                    );
                }
            }
            
            await next();
        });

        return app;
    }

    public static async Task InitializeDatabaseAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
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
        catch (Exception ex) when (IsDatabaseException(ex))
        {
            logger.LogError(ex, "Unable to initialize/migrate database");
            
            // Try to create the database if it doesn't exist
            await TryCreateDatabaseAsync(dbContext, logger);
        }
    }

    private static bool IsDatabaseException(Exception ex)
    {
        return ex is PostgresException ||
               ex is NpgsqlException ||
               ex is SocketException ||
               (ex.InnerException is PostgresException) ||
               (ex.InnerException is NpgsqlException) ||
               (ex.InnerException is SocketException);
    }

    private static string SanitizeConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return $"Host={builder.Host};Database={builder.Database};Port={builder.Port}";
        }
        catch
        {
            return "Connection string parsing failed";
        }
    }

    private static async Task TryCreateDatabaseAsync(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            var connectionString = dbContext.Database.GetConnectionString() ?? "";
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
}
