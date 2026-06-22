using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Publisher;

public record PublisherProfileResponse(
    int Id,
    string Nombre,
    string Email,
    string? Telefono,
    string? LogoUrl,
    TipoPublicador Tipo,
    int TotalPublicaciones
);
