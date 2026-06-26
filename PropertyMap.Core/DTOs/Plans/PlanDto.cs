namespace PropertyMap.Core.DTOs.Plans;

public record PlanDto(
    int Id,
    string Nombre,
    string Slug,
    decimal PrecioMensual,
    string Moneda,
    int? MaxPublicaciones,
    int DestacadosIncluidos,
    bool EstadisticasAvanzadas
);
