using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Alerts;

public record CreateAlertRequest(
    string? Nombre,
    TipoOperacion? Operacion,
    TipoPropiedad? TipoPropiedad,
    string? Ciudad,
    decimal? PrecioMax,
    string? Moneda,
    int? DormitoriosMin
);
