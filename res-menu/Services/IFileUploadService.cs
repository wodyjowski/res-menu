namespace res_menu.Services;

public interface IFileUploadService
{
    /// <summary>
    /// Upload a file and return the uploaded file information
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <param name="uploadType">Type of upload (logo, menu-item)</param>
    /// <returns>UploadResult containing success status and file URL or error messages</returns>
    Task<UploadResult> UploadFileAsync(IFormFile file, UploadType uploadType);

    /// <summary>
    /// Delete a file from the server
    /// </summary>
    /// <param name="fileUrl">The URL of the file to delete</param>
    /// <returns>True if file was deleted successfully</returns>
    Task<bool> DeleteFileAsync(string fileUrl);

    /// <summary>
    /// Validate file before upload
    /// </summary>
    /// <param name="file">The file to validate</param>
    /// <param name="uploadType">Type of upload for specific validation rules</param>
    /// <returns>ValidationResult with any error messages</returns>
    ValidationResult ValidateFile(IFormFile file, UploadType uploadType);
}

public enum UploadType
{
    Logo,
    MenuItem
}

public class UploadResult
{
    public bool Success { get; set; }
    public string? FileUrl { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
