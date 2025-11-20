using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LeaveManagement.Services
{
    public class LocalFileStore : IFileStore
    {
        private readonly string _basePath;
        private readonly long _maxBytes;
        private readonly string[] _allowedExtensions;

        public LocalFileStore(string basePath, IConfiguration configuration)
        {
            _basePath = basePath;
            Directory.CreateDirectory(_basePath);
            _maxBytes = configuration.GetValue<long>("LocalFileStore:MaxFileBytes", 5_242_880);
            // More permissive list of allowed extensions including common document and image formats
            _allowedExtensions = (configuration.GetValue<string>("LocalFileStore:AllowedExtensions") ?? ".pdf,.doc,.docx,.xls,.xlsx,.txt,.png,.jpg,.jpeg,.gif,.bmp,.zip,.rar,.7z").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).ToArray();
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType)
        {
            // Validate file size first
            if (fileStream.Length == 0) throw new InvalidOperationException("File is empty");
            if (fileStream.Length > _maxBytes) throw new InvalidOperationException("File too large");

            // Get file extension
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            
            // If extension is empty, try to infer from content type
            if (string.IsNullOrEmpty(ext))
            {
                ext = GetExtensionFromContentType(contentType);
            }

            // Check if file type is allowed
            if (!string.IsNullOrEmpty(ext) && !_allowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException($"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");
            }

            var safeName = Path.GetFileName(fileName);
            var newName = $"{Guid.NewGuid():N}_{safeName}";
            var path = Path.Combine(_basePath, newName);

            using var fs = File.Create(path);
            fileStream.Position = 0;
            await fileStream.CopyToAsync(fs);
            return newName;
        }

        private string GetExtensionFromContentType(string contentType)
        {
            return contentType switch
            {
                "application/pdf" => ".pdf",
                "application/msword" => ".doc",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "application/vnd.ms-excel" => ".xls",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
                "text/plain" => ".txt",
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "application/zip" => ".zip",
                _ => ""
            };
        }

        public Task<Stream> GetFileAsync(string filePath)
        {
            var path = Path.Combine(_basePath, filePath);
            Stream s = File.OpenRead(path);
            return Task.FromResult(s);
        }

        public string GetPublicUrl(string filePath)
        {
            // For local dev, files served from /uploads via static files mapping
            return $"/uploads/{Uri.EscapeDataString(filePath)}";
        }
    }
}