using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Plans;

public record SubscriptionDto(
    int Id,
    int PlanId,
    string PlanNombre,
    EstadoSuscripcion Estado,
    DateTime FechaInicio,
    DateTime FechaVencimiento,
    bool AutoRenovar
);
