using ResMenu.Services;

namespace ResMenu.Extensions
{
    public static class HttpContextExtensions
    {
        public static string BuildMenuUrl(this HttpContext context, string subdomain)
        {
            var portService = context.RequestServices.GetRequiredService<IDynamicPortService>();
            return portService.BuildExternalUrl(context, subdomain);
        }
        
        public static int GetDynamicPort(this HttpContext context)
        {
            var portService = context.RequestServices.GetRequiredService<IDynamicPortService>();
            return portService.GetCurrentPort(context);
        }
    }
}
