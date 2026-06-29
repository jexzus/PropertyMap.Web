using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record CreateListingRequest(
    TipoOperacion Operacion,
    TipoPropiedad TipoPropiedad,
    [StringLength(150)] string Titulo,
    [StringLength(5000)] string Descripcion,
    decimal Precio,
    [StringLength(10)] string Moneda,
    [StringLength(200)] string DireccionTexto,
    [StringLength(200)] string Ciudad,
    [StringLength(200)] string Provincia,
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
