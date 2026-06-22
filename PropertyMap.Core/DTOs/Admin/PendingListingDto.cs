namespace PropertyMap.Core.DTOs.Admin;

public record PendingListingDto(
    int Id,
    string Titulo,
    string DireccionTexto,
    string Ciudad,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string? FotoPrincipalUrl,
    string PublisherNombre,
    string PublisherEmail,
    DateTime FechaPublicacion
);
