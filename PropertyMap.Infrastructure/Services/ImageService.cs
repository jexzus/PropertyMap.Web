using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Infrastructure.Services;

public class ImageService : IImageService
{
    private readonly string _uploadsRoot;
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ImageService(IConfiguration config)
    {
        _uploadsRoot = config["ImageSettings:UploadsRoot"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    }

    public async Task<List<string>> SaveImagesAsync(IFormFileCollection files, int listingId)
    {
        var dir = Path.Combine(_uploadsRoot, "properties", listingId.ToString());
        Directory.CreateDirectory(dir);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > MaxFileSizeBytes) continue;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            await using var stream = File.Create(fullPath);
            await file.CopyToAsync(stream);

            urls.Add($"/uploads/properties/{listingId}/{fileName}");
        }

        return urls;
    }

    public Task DeleteImageAsync(string relativeUrl)
    {
        var fullPath = Path.Combine(_uploadsRoot, "..", relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task DeleteAllImagesForListingAsync(int listingId)
    {
        var dir = Path.Combine(_uploadsRoot, "properties", listingId.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    public async Task<string> SaveAvatarAsync(string userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Extensión no permitida: {ext}");
        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("El archivo supera el límite de 10 MB.");

        var dir = Path.Combine(_uploadsRoot, "avatars", userId);

        if (Directory.Exists(dir))
            foreach (var existing in Directory.GetFiles(dir))
                File.Delete(existing);

        Directory.CreateDirectory(dir);

        var fileName = $"avatar{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/uploads/avatars/{userId}/{fileName}";
    }
}
