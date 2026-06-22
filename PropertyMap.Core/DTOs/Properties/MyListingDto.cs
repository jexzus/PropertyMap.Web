namespace PropertyMap.Core.DTOs.Properties;

public record MyListingDto(
    int Id,
    string Titulo,
    string DireccionTexto,
    string Ciudad,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string Estado,
    string? FotoPrincipalUrl,
    DateTime FechaPublicacion,
    DateTime FechaActualizacion
);
