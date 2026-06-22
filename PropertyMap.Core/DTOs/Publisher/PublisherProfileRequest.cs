using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Publisher;

public record PublisherProfileRequest(
    string Nombre,
    string Telefono,
    TipoPublicador Tipo
);
