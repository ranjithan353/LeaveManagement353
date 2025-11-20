namespace LeaveManagement.Services
{
    public interface IFileStore
    {
        Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType);
        Task<Stream> GetFileAsync(string filePath);
        string GetPublicUrl(string filePath);
    }
}