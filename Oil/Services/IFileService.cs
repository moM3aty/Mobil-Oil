using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Oil.Services
{
    public interface IFileService
    {
        Task<string?> SaveFileAsync(IFormFile? file, string subfolder, string[]? allowedExtensions = null, long maxFileSize = 5 * 1024 * 1024);
        void DeleteFile(string? relativePath);
    }
}
