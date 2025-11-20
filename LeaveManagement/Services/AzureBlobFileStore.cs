using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace LeaveManagement.Services
{
    public class AzureBlobFileStore : IFileStore
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobFileStore> _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobFileStore(IConfiguration configuration, ILogger<AzureBlobFileStore> logger)
        {
            _logger = logger;
            var connectionString = configuration.GetConnectionString("AzureBlobStorage");
            var containerName = configuration["AzureBlobStorage:ContainerName"] ?? "attachments";

            _blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            try
            {
                _containerClient.CreateIfNotExists();
                _logger.LogInformation("Blob container '{ContainerName}' verified/created", containerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create container '{ContainerName}', it may already exist", containerName);
            }
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                // Generate unique blob name to avoid collisions
                var blobName = $"{Guid.NewGuid()}_{fileName}";
                var blobClient = _containerClient.GetBlobClient(blobName);

                // Upload file to blob storage
                await blobClient.UploadAsync(fileStream, overwrite: true);

                _logger.LogInformation($"File {fileName} uploaded to blob storage as {blobName}");

                // Return the blob URI
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file to blob storage: {ex.Message}");
                throw;
            }
        }

        public async Task<Stream> GetFileAsync(string filePath)
        {
            try
            {
                // Extract blob name from full URI path
                var uri = new Uri(filePath);
                var blobName = uri.Segments.Last();

                var blobClient = _containerClient.GetBlobClient(blobName);
                var download = await blobClient.DownloadAsync();

                return download.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading file from blob storage: {ex.Message}");
                throw;
            }
        }

        public string GetPublicUrl(string filePath)
        {
            try
            {
                // If filePath is already a full URI, extract the blob name
                if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri))
                {
                    var blobName = uri.Segments.Last();
                    var blobClient = _containerClient.GetBlobClient(blobName);
                    
                    // Generate SAS token for public access (valid for 1 year)
                    if (blobClient.CanGenerateSasUri)
                    {
                        var sasBuilder = new BlobSasBuilder
                        {
                            BlobContainerName = _containerClient.Name,
                            BlobName = blobName,
                            Resource = "b", // blob
                            ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                        };
                        sasBuilder.SetPermissions(BlobSasPermissions.Read);
                        
                        var sasUri = blobClient.GenerateSasUri(sasBuilder);
                        _logger.LogInformation("Generated SAS URL for blob: {BlobName}", blobName);
                        return sasUri.ToString();
                    }
                    else
                    {
                        // Fallback to direct URI (requires container to be public)
                        _logger.LogWarning("Cannot generate SAS token for blob: {BlobName}, using direct URI", blobName);
                        return blobClient.Uri.ToString();
                    }
                }
                
                // If filePath is just a blob name, construct the client and generate SAS
                var blobClientByName = _containerClient.GetBlobClient(filePath);
                if (blobClientByName.CanGenerateSasUri)
                {
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = _containerClient.Name,
                        BlobName = filePath,
                        Resource = "b",
                        ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);
                    
                    var sasUri = blobClientByName.GenerateSasUri(sasBuilder);
                    return sasUri.ToString();
                }
                
                return blobClientByName.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating public URL for file: {FilePath}", filePath);
                // Fallback to original filePath
                return filePath;
            }
        }
    }
}
