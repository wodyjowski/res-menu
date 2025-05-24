using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace res_menu.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public bool IsDbError { get; set; }

    public void OnGet(string? errorType = null)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        IsDbError = errorType == "database";
        
        if (IsDbError)
        {
            _logger.LogError("Database connection error detected. RequestId: {RequestId}", RequestId);
        }
    }
}