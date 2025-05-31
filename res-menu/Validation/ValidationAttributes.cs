using System.ComponentModel.DataAnnotations;

namespace res_menu.Validation;

/// <summary>
/// Custom validation attribute for file uploads
/// </summary>
public class FileUploadValidationAttribute : ValidationAttribute
{
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5MB default
    public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    public bool IsRequired { get; set; } = false;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var file = value as IFormFile;

        // If file is null and not required, it's valid
        if (file == null)
        {
            return IsRequired 
                ? new ValidationResult("A file is required.")
                : ValidationResult.Success;
        }

        // Check file size
        if (file.Length > MaxFileSizeBytes)
        {
            return new ValidationResult($"File size cannot exceed {MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return new ValidationResult($"File type '{extension}' is not supported. Allowed types: {string.Join(", ", AllowedExtensions)}");
        }

        // Check content type
        var validContentTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!validContentTypes.Contains(file.ContentType?.ToLowerInvariant()))
        {
            return new ValidationResult("Invalid file content type. Please upload a valid image file.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Custom validation attribute for price fields
/// </summary>
public class PriceValidationAttribute : ValidationAttribute
{
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 10000;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return new ValidationResult("Price is required.");
        }        if (value is decimal price)
        {
            if (price < (decimal)MinValue)
            {
                return new ValidationResult($"Price cannot be less than {MinValue:C}.");
            }

            if (price > (decimal)MaxValue)
            {
                return new ValidationResult($"Price cannot exceed {MaxValue:C}.");
            }

            // Check for reasonable decimal places (max 2)
            if (decimal.Round(price, 2) != price)
            {
                return new ValidationResult("Price can have at most 2 decimal places.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Custom validation attribute for subdomain fields
/// </summary>
public class SubdomainValidationAttribute : ValidationAttribute
{
    private static readonly string[] ReservedSubdomains = 
    {
        "www", "api", "admin", "mail", "email", "ftp", "ssh", "root", "login", "register",
        "dashboard", "panel", "support", "help", "blog", "news", "app", "mobile",
        "test", "staging", "dev", "development", "prod", "production"
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var subdomain = value as string;

        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return new ValidationResult("Subdomain is required.");
        }

        // Check length
        if (subdomain.Length < 3)
        {
            return new ValidationResult("Subdomain must be at least 3 characters long.");
        }

        if (subdomain.Length > 50)
        {
            return new ValidationResult("Subdomain cannot exceed 50 characters.");
        }

        // Check format
        if (!System.Text.RegularExpressions.Regex.IsMatch(subdomain, @"^[a-zA-Z0-9-]+$"))
        {
            return new ValidationResult("Subdomain can only contain letters, numbers, and hyphens.");
        }

        // Check if it starts or ends with hyphen
        if (subdomain.StartsWith("-") || subdomain.EndsWith("-"))
        {
            return new ValidationResult("Subdomain cannot start or end with a hyphen.");
        }

        // Check reserved words
        if (ReservedSubdomains.Contains(subdomain.ToLowerInvariant()))
        {
            return new ValidationResult($"'{subdomain}' is a reserved subdomain and cannot be used.");
        }

        return ValidationResult.Success;
    }
}
