namespace PropertyMap.Core.DTOs.Stats;

public record ListingStatsDto(
    int ListingId,
    string Titulo,
    int Vistas,
    int Favoritos,
    int Consultas,
    int Conversiones
);
