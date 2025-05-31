using System.Net;
using Microsoft.EntityFrameworkCore;

namespace res_menu.Services;

public interface IErrorHandlingService
{
    /// <summary>
    /// Handle exceptions and return appropriate HTTP status codes and messages
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <returns>Error response with status code and message</returns>
    ErrorResponse HandleException(Exception exception);

    /// <summary>
    /// Log exception with appropriate level based on exception type
    /// </summary>
    /// <param name="exception">Exception to log</param>
    /// <param name="context">Additional context information</param>
    void LogException(Exception exception, string? context = null);

    /// <summary>
    /// Create user-friendly error message from exception
    /// </summary>
    /// <param name="exception">Exception to process</param>
    /// <returns>User-friendly error message</returns>
    string GetUserFriendlyMessage(Exception exception);
}

public class ErrorResponse
{
    public HttpStatusCode StatusCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TechnicalDetails { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ErrorHandlingService : IErrorHandlingService
{
    private readonly ILogger<ErrorHandlingService> _logger;
    private readonly IWebHostEnvironment _environment;

    public ErrorHandlingService(ILogger<ErrorHandlingService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public ErrorResponse HandleException(Exception exception)
    {
        var response = new ErrorResponse();

        switch (exception)
        {
            case FileNotFoundException:
                response.StatusCode = HttpStatusCode.NotFound;
                response.Title = "File Not Found";
                response.Message = "The requested file could not be found.";
                break;

            case UnauthorizedAccessException:
                response.StatusCode = HttpStatusCode.Forbidden;
                response.Title = "Access Denied";
                response.Message = "You don't have permission to access this resource.";
                break;

            case InvalidOperationException when exception.Message.Contains("subdomain"):
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Title = "Invalid Subdomain";
                response.Message = "The specified subdomain is invalid or already in use.";
                break;

            case ArgumentException:
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Title = "Invalid Request";
                response.Message = "The request contains invalid data. Please check your input and try again.";
                break;

            case TimeoutException:
                response.StatusCode = HttpStatusCode.RequestTimeout;
                response.Title = "Request Timeout";
                response.Message = "The request took too long to complete. Please try again.";
                break;

            case DbUpdateException:
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.Title = "Database Error";
                response.Message = "A database error occurred. Please try again later.";
                break;

            case HttpRequestException:
                response.StatusCode = HttpStatusCode.BadGateway;
                response.Title = "External Service Error";
                response.Message = "An external service is currently unavailable. Please try again later.";
                break;

            default:
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.Title = "Internal Server Error";
                response.Message = "An unexpected error occurred. Please try again later.";
                break;
        }

        // Include technical details in development environment
        if (_environment.IsDevelopment())
        {
            response.TechnicalDetails = exception.ToString();
        }

        return response;
    }

    public void LogException(Exception exception, string? context = null)
    {
        var contextMessage = string.IsNullOrEmpty(context) ? string.Empty : $" Context: {context}";

        switch (exception)
        {
            case ArgumentException:
            case InvalidOperationException:
                _logger.LogWarning(exception, "Validation error occurred.{Context}", contextMessage);
                break;

            case FileNotFoundException:
            case UnauthorizedAccessException:
                _logger.LogWarning(exception, "Resource access error.{Context}", contextMessage);
                break;

            case TimeoutException:
                _logger.LogWarning(exception, "Timeout error occurred.{Context}", contextMessage);
                break;

            case DbUpdateException:
            case HttpRequestException:
                _logger.LogError(exception, "Service error occurred.{Context}", contextMessage);
                break;

            default:
                _logger.LogError(exception, "Unexpected error occurred.{Context}", contextMessage);
                break;
        }
    }

    public string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "The requested file could not be found.",
            UnauthorizedAccessException => "You don't have permission to perform this action.",
            InvalidOperationException when exception.Message.Contains("subdomain") => "This subdomain is already taken or invalid.",
            ArgumentException => "Please check your input and try again.",
            TimeoutException => "The operation took too long. Please try again.",
            DbUpdateException => "A database error occurred. Please try again later.",
            HttpRequestException => "An external service is unavailable. Please try again later.",
            _ => "An unexpected error occurred. Please try again later."
        };
    }
}
