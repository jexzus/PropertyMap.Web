using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Publisher;

namespace PropertyMap.Web.Services;

public interface IPropertyApiService
{
    Task<int> CreateListingAsync(CreateListingRequest request);
    Task<List<string>> UploadImagesAsync(int listingId,
        IEnumerable<(byte[] Data, string FileName, string ContentType)> files);
    Task<List<MyListingDto>> GetMyListingsAsync();
    Task<PublisherProfileResponse?> GetPublisherProfileAsync();
    Task<int> EnsurePublisherProfileAsync(string nombre, string telefono);
}
