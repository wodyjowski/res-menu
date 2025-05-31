using res_menu.Services;
using System.Text.Json;

namespace res_menu.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorHandlingService = context.RequestServices.GetRequiredService<IErrorHandlingService>();
        var errorResponse = errorHandlingService.HandleException(exception);

        errorHandlingService.LogException(exception, 
            $"Path: {context.Request.Path}, Method: {context.Request.Method}");

        context.Response.StatusCode = (int)errorResponse.StatusCode;
        context.Response.ContentType = "application/json";

        var jsonResponse = JsonSerializer.Serialize(new
        {
            error = new
            {
                title = errorResponse.Title,
                message = errorResponse.Message,
                timestamp = errorResponse.Timestamp,
                technicalDetails = errorResponse.TechnicalDetails
            }
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
