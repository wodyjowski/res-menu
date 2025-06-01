using Microsoft.AspNetCore.Http;

namespace res_menu.Middleware;

public class SubdomainResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubdomainResolutionMiddleware> _logger;

    public SubdomainResolutionMiddleware(RequestDelegate next, ILogger<SubdomainResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = _logger;
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
            // Always store the subdomain in HttpContext.Items and query string
            context.Items["Subdomain"] = detectedSubdomain;
            var queryString = context.Request.QueryString.Add("subdomain", detectedSubdomain);
            context.Request.QueryString = queryString;

            // Check if this is a static file request
            var path = context.Request.Path.Value?.ToLower();
            var isStaticFile = !string.IsNullOrEmpty(path) && (
                path.StartsWith("/lib/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/images/") ||
                path.StartsWith("/uploads/") ||
                path.StartsWith("/favicon.ico") ||
                path.EndsWith(".css") ||
                path.EndsWith(".js") ||
                path.EndsWith(".png") ||
                path.EndsWith(".jpg") ||
                path.EndsWith(".jpeg") ||
                path.EndsWith(".gif") ||
                path.EndsWith(".svg") ||
                path.EndsWith(".ico") ||
                path.EndsWith(".woff") ||
                path.EndsWith(".woff2") ||
                path.EndsWith(".ttf") ||
                path.EndsWith(".eot")
            );            if (!isStaticFile)
            {
                var currentPath = context.Request.Path.Value?.ToLower();
                
                // Handle Menu pages - rewrite to include subdomain if needed
                if (currentPath?.StartsWith("/menu") == true)
                {
                    var expectedPath = $"/menu/{detectedSubdomain.ToLower()}";
                    
                    if (currentPath != expectedPath)
                    {
                        logger.LogInformation("Changing request path from {CurrentPath} to include subdomain: {Subdomain}", currentPath, detectedSubdomain);
                        context.Request.Path = $"/Menu/{detectedSubdomain}";
                    }
                    else
                    {
                        logger.LogInformation("Path already correctly formatted for subdomain: {Path}", currentPath);
                    }
                }
                // Handle Order pages - preserve subdomain context but don't rewrite path
                else if (currentPath?.StartsWith("/order") == true)
                {
                    logger.LogInformation("Order page accessed with subdomain context: {Subdomain}, Path: {Path}", detectedSubdomain, currentPath);
                }
                // Handle other pages - rewrite to Menu
                else
                {
                    logger.LogInformation("Redirecting unknown path to Menu for subdomain: {Subdomain}, Path: {CurrentPath}", detectedSubdomain, currentPath);
                    context.Request.Path = $"/Menu/{detectedSubdomain}";
                }
                
                logger.LogInformation(
                    "Subdomain Middleware - Request processed for subdomain. Path: {Path}, Subdomain Item: {SubdomainItem}, QueryString: {QueryString}",
                    context.Request.Path,
                    context.Items["Subdomain"],
                    context.Request.QueryString.ToString()
                );
            }
            else
            {
                logger.LogInformation("Static file request detected, not rewriting path: {Path}", path);
            }
        }
        else if (isLocalhost && context.Request.Path.StartsWithSegments("/Menu", StringComparison.OrdinalIgnoreCase))
        {
            // For localhost testing
            var subdomainParam = context.Request.Query["subdomain"].FirstOrDefault();
            if (!string.IsNullOrEmpty(subdomainParam))
            {
                context.Items["Subdomain"] = subdomainParam;
                logger.LogInformation(
                    "Subdomain Middleware - Localhost /Menu?subdomain=... detected. Set HttpContext.Items[\"Subdomain\"] = {Subdomain}",
                    subdomainParam
                );
            }
        }
        
        await _next(context);
    }

    private string? ResolveSubdomain(HttpContext context)
    {
        // First try from query string
        var subdomain = context.Request.Query["subdomain"].FirstOrDefault();
        if (!string.IsNullOrEmpty(subdomain))
        {
            _logger.LogInformation("Subdomain found in query string: {Subdomain}", subdomain);
            return subdomain;
        }

        // Then try from host
        var host = context.Request.Host.Host;
        _logger.LogInformation("Attempting to extract subdomain from host: {Host}", host);

        // Handle duckdns.org domain
        if (host.EndsWith(".res-menu.duckdns.org"))
        {
            var parts = host.Split('.');
            if (parts.Length > 3 && parts[0] != "www") // Ensure it's not www and has subdomain.res-menu.duckdns.org
            {
                subdomain = parts[0];
                _logger.LogInformation("Extracted subdomain from duckdns host: {Subdomain}", subdomain);
                return subdomain;
            }
        }
        // Handle localhost development
        else if (host.Contains(".localhost"))
        {
            var parts = host.Split('.');
            if (parts.Length > 1 && parts[0] != "www" && parts[0] != "localhost")
            {
                subdomain = parts[0];
                _logger.LogInformation("Extracted subdomain from localhost: {Subdomain}", subdomain);
                return subdomain;
            }
        }

        // Finally try from path segments
        var path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0].Equals("Menu", StringComparison.OrdinalIgnoreCase))
            {
                subdomain = segments[1];
                _logger.LogInformation("Extracted subdomain from path: {Subdomain}", subdomain);
                return subdomain;
            }
        }

        _logger.LogInformation("No subdomain found in request");
        return null;
    }
}

public static class SubdomainResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseSubdomainResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubdomainResolutionMiddleware>();
    }
} 