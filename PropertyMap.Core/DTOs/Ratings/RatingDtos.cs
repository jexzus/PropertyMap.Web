// PropertyMap.Core/DTOs/Ratings/RatingDtos.cs
using System.ComponentModel.DataAnnotations;

namespace PropertyMap.Core.DTOs.Ratings;

public record RatePropertyRequest(
    int ListingId,
    [Range(1, 5)] int PuntajeUbicacion,
    [Range(1, 5)] int PuntajeEstado,
    [Range(1, 5)] int PuntajePrecioCalidad,
    string? Comentario);

public record RateAgentRequest(
    int PublisherId,
    [Range(1, 5)] int PuntajeAtencion,
    [Range(1, 5)] int PuntajeRapidez,
    [Range(1, 5)] int PuntajeTransparencia,
    [Range(1, 5)] int PuntajeProfesionalismo,
    string? Comentario);

public record PropertyRatingStatsDto(
    double PromedioUbicacion,
    double PromedioEstado,
    double PrecioCal,
    double PromedioGeneral,
    int TotalValoraciones);

public record AgentRatingStatsDto(
    double PromedioAtencion,
    double PromedioRapidez,
    double PromedioTransparencia,
    double PromedioProfesionalismo,
    double PromedioGeneral,
    int TotalValoraciones);

public record AgentRankingItemDto(
    int PublisherId,
    string Nombre,
    string Tipo,
    string? LogoUrl,
    double RankingScore,
    double RatingPromedio,
    double TiempoRespuestaHoras,
    int Operaciones,
    double AntiguedadAnios);
