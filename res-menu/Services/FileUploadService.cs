namespace res_menu.Services;

public class FileUploadService : IFileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;

    // Configuration for different upload types
    private readonly Dictionary<UploadType, UploadConfiguration> _uploadConfigs = new()
    {
        {
            UploadType.Logo,
            new UploadConfiguration
            {
                MaxFileSizeBytes = 5 * 1024 * 1024, // 5MB
                AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
                UploadPath = "uploads/logos",
                MaxDimensions = new ImageDimensions { Width = 2000, Height = 2000 }
            }
        },
        {
            UploadType.MenuItem,
            new UploadConfiguration
            {
                MaxFileSizeBytes = 10 * 1024 * 1024, // 10MB
                AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" },
                UploadPath = "uploads/menu-items",
                MaxDimensions = new ImageDimensions { Width = 3000, Height = 3000 }
            }
        }
    };

    public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<UploadResult> UploadFileAsync(IFormFile file, UploadType uploadType)
    {
        var result = new UploadResult();

        try
        {
            // Validate the file first
            var validation = ValidateFile(file, uploadType);
            if (!validation.IsValid)
            {
                result.Errors.AddRange(validation.Errors);
                return result;
            }

            var config = _uploadConfigs[uploadType];
            var uploadsFolder = Path.Combine(_environment.WebRootPath, config.UploadPath);
            
            // Ensure directory exists
            Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Additional image processing if needed (resize, optimize)
            await ProcessImageAsync(filePath, config);

            result.Success = true;
            result.FileUrl = $"/{config.UploadPath}/{uniqueFileName}";
            result.FileName = uniqueFileName;
            result.FileSize = file.Length;

            _logger.LogInformation("File uploaded successfully: {FileName} ({FileSize} bytes)", 
                uniqueFileName, file.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            result.Errors.Add("An error occurred while uploading the file. Please try again.");
        }

        return result;
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(fileUrl))
                return true;

            var filePath = Path.Combine(_environment.WebRootPath, 
                fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                return true;
            }

            return true; // File doesn't exist, consider it "deleted"
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileUrl}", fileUrl);
            return false;
        }
    }

    public ValidationResult ValidateFile(IFormFile file, UploadType uploadType)
    {
        var result = new ValidationResult { IsValid = true };

        if (file == null)
        {
            result.IsValid = false;
            result.Errors.Add("No file was selected.");
            return result;
        }

        var config = _uploadConfigs[uploadType];

        // Check file size
        if (file.Length > config.MaxFileSizeBytes)
        {
            result.IsValid = false;
            result.Errors.Add($"File size exceeds the maximum limit of {config.MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        // Check file extension
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!config.AllowedExtensions.Contains(fileExtension))
        {
            result.IsValid = false;
            result.Errors.Add($"File type '{fileExtension}' is not supported. Allowed types: {string.Join(", ", config.AllowedExtensions)}");
        }

        // Check content type
        if (!IsValidImageContentType(file.ContentType))
        {
            result.IsValid = false;
            result.Errors.Add("Invalid file content type. Please upload a valid image file.");
        }

        // Additional security check: validate file signature
        if (!IsValidImageFileSignature(file))
        {
            result.IsValid = false;
            result.Errors.Add("File appears to be corrupted or is not a valid image file.");
        }

        return result;
    }

    private static bool IsValidImageContentType(string contentType)
    {
        var validContentTypes = new[]
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
        };

        return validContentTypes.Contains(contentType?.ToLowerInvariant());
    }

    private static bool IsValidImageFileSignature(IFormFile file)
    {
        try
        {
            using var reader = new BinaryReader(file.OpenReadStream());
            
            // Read the first few bytes to check file signature
            var signature = reader.ReadBytes(8);
            
            // Check for common image file signatures
            // JPEG: FF D8 FF
            if (signature.Length >= 3 && signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF)
                return true;
            
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (signature.Length >= 8 && signature[0] == 0x89 && signature[1] == 0x50 && 
                signature[2] == 0x4E && signature[3] == 0x47)
                return true;
            
            // GIF: 47 49 46 38
            if (signature.Length >= 4 && signature[0] == 0x47 && signature[1] == 0x49 && 
                signature[2] == 0x46 && signature[3] == 0x38)
                return true;
            
            // WebP: 52 49 46 46 ... 57 45 42 50
            if (signature.Length >= 4 && signature[0] == 0x52 && signature[1] == 0x49 && 
                signature[2] == 0x46 && signature[3] == 0x46)
                return true;

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Reset the stream position
            file.OpenReadStream().Position = 0;
        }
    }

    private async Task ProcessImageAsync(string filePath, UploadConfiguration config)
    {
        try
        {
            // Here you could add image processing logic like:
            // - Resize images that exceed max dimensions
            // - Compress images to reduce file size
            // - Convert formats if needed
            // - Add watermarks
            
            // For now, we'll just log that processing could be done here
            _logger.LogDebug("Image processing placeholder for: {FilePath}", filePath);
            
            // Example: You could use libraries like ImageSharp, SkiaSharp, or System.Drawing
            // to process images here
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image processing failed for: {FilePath}", filePath);
            // Don't throw - the file upload was successful even if processing failed
        }
    }
}

public class UploadConfiguration
{
    public long MaxFileSizeBytes { get; set; }
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string UploadPath { get; set; } = string.Empty;
    public ImageDimensions? MaxDimensions { get; set; }
}

public class ImageDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
}
