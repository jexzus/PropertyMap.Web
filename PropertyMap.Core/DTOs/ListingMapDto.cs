namespace PropertyMap.Core.DTOs;

public record ListingMapDto(
    int Id,
    double Lat,
    double Lng,
    string Titulo,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string? FotoUrl
);
