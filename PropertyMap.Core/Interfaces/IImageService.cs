using Microsoft.AspNetCore.Http;

namespace PropertyMap.Core.Interfaces;

public interface IImageService
{
    Task<List<string>> SaveImagesAsync(IFormFileCollection files, int listingId);
    Task DeleteImageAsync(string relativeUrl);
    Task DeleteAllImagesForListingAsync(int listingId);
    Task<string> SaveAvatarAsync(string userId, IFormFile file);
}
