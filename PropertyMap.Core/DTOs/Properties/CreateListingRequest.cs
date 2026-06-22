using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record CreateListingRequest(
    TipoOperacion Operacion,
    TipoPropiedad TipoPropiedad,
    string Titulo,
    string Descripcion,
    decimal Precio,
    string Moneda,
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
    List<string> Amenities
);
