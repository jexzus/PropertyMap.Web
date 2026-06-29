using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Alerts;

public record CreateAlertRequest(
    [StringLength(100)] string? Nombre,
    TipoOperacion? Operacion,
    TipoPropiedad? TipoPropiedad,
    [StringLength(200)] string? Ciudad,
    decimal? PrecioMax,
    string? Moneda,
    int? DormitoriosMin
);
