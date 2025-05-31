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
        var subdomain = ResolveSubdomain(context);
        if (!string.IsNullOrEmpty(subdomain))
        {
            context.Items["Subdomain"] = subdomain;
            _logger.LogInformation("Resolved subdomain: {Subdomain}", subdomain);
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