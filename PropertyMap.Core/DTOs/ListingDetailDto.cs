namespace PropertyMap.Core.DTOs;

public record ListingDetailDto(
    int Id,
    string Titulo,
    string Descripcion,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string DireccionTexto,
    string Ciudad,
    string Provincia,
    double Lat,
    double Lng,
    decimal? Superficie,
    decimal? SuperficieCubierta,
    int? Ambientes,
    int? Dormitorios,
    int? Banos,
    int? Antiguedad,
    bool Cochera,
    List<string> Amenities,
    List<string> FotoUrls,
    string PublisherNombre,
    string? PublisherTelefono,
    string? PublisherLogoUrl,
    DateTime FechaPublicacion
);
