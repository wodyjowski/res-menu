using Microsoft.AspNetCore.Http;

namespace ResMenu.Services
{
    public interface IDynamicPortService
    {
        int GetCurrentPort(HttpContext context);
        string BuildExternalUrl(HttpContext context, string subdomain);
        void StorePortInCookie(HttpContext context, int port);
        int? GetPortFromCookie(HttpContext context);
    }

    public class DynamicPortService : IDynamicPortService
    {
        private const string PORT_COOKIE_NAME = "ResMenuPort";
        private const int COOKIE_EXPIRY_DAYS = 30;
        
        public int GetCurrentPort(HttpContext context)
        {
            // First try to get from cookie
            var cookiePort = GetPortFromCookie(context);
            if (cookiePort.HasValue && cookiePort.Value > 0)
            {
                return cookiePort.Value;
            }
            
            // If no cookie, detect from current request
            var currentPort = context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80);
            
            // Store the detected port in cookie for future use
            StorePortInCookie(context, currentPort);
            
            return currentPort;
        }

        public string BuildExternalUrl(HttpContext context, string subdomain)
        {
            var isLocalhost = IsLocalhost(context.Request.Host.Host);
            
            if (isLocalhost)
            {
                // For localhost, use query parameter approach
                return $"{context.Request.Scheme}://{context.Request.Host}/Menu?subdomain={subdomain}";
            }
            
            // For production, use subdomain with dynamic port detection
            var port = GetCurrentPort(context);
            var scheme = context.Request.IsHttps ? "https" : "http";
            
            // Only include port if it's not the default port for the scheme
            var portSuffix = "";
            if ((scheme == "http" && port != 80) || (scheme == "https" && port != 443))
            {
                portSuffix = $":{port}";
            }
            
            return $"{scheme}://{subdomain}.res-menu.duckdns.org{portSuffix}";
        }

        public void StorePortInCookie(HttpContext context, int port)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(COOKIE_EXPIRY_DAYS)
            };
            
            context.Response.Cookies.Append(PORT_COOKIE_NAME, port.ToString(), cookieOptions);
        }

        public int? GetPortFromCookie(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue(PORT_COOKIE_NAME, out var portValue) && 
                int.TryParse(portValue, out var port) && 
                port > 0)
            {
                return port;
            }
            
            return null;
        }

        private static bool IsLocalhost(string host)
        {
            var hostLower = host.ToLower();
            return hostLower == "localhost" || 
                   hostLower.StartsWith("127.") || 
                   hostLower.StartsWith("192.168.") ||
                   hostLower.StartsWith("10.") ||
                   hostLower.StartsWith("172.");
        }
    }
}
