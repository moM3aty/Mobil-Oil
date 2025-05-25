using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Oil.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _uploadsRootFolder;

        public FileService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            _uploadsRootFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");

            if (!Directory.Exists(_uploadsRootFolder))
            {
                Directory.CreateDirectory(_uploadsRootFolder);
            }
        }

        public async Task<string?> SaveFileAsync(IFormFile? file, string subfolder, string[]? allowedExtensions = null, long maxFileSize = 5 * 1024 * 1024) // 5MB default max size
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > maxFileSize)
                throw new ArgumentException($"حجم الملف يتجاوز الحد الأقصى المسموح به ({maxFileSize / (1024 * 1024)} MB).");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (allowedExtensions != null && allowedExtensions.Any() && !allowedExtensions.Contains(extension))
                throw new ArgumentException($"نوع الملف غير مسموح به. الأنواع المسموح بها: {string.Join(", ", allowedExtensions)}");

            var targetFolder = Path.Combine(_uploadsRootFolder, subfolder);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string filePath = Path.Combine(targetFolder, uniqueFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                return $"/uploads/{subfolder}/{uniqueFileName}"; // Return relative path
            }
            catch (Exception ex)
            {
                // Log ex here (using a proper logging framework)
                Console.Error.WriteLine($"Error saving file {file.FileName}: {ex.Message}");
                throw; // Re-throw to allow controller to handle it or global handler
            }
        }

        public void DeleteFile(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            // Convert relative path to absolute path
            // Ensure the path starts with / for TrimStart('/') to work correctly if webRootPath already ends with /
            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                // Log ex here
                Console.Error.WriteLine($"Error deleting file {relativePath}: {ex.Message}");
                // Decide if you want to re-throw or handle silently
            }
        }
    }
}