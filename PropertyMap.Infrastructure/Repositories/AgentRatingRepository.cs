using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AgentRatingRepository : IAgentRatingRepository
{
    private readonly AppDbContext _ctx;

    public AgentRatingRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<bool> HasConsultaWithPublisherAsync(int publisherId, string userId)
    {
        var publisherListingIds = await _ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Select(l => l.Id)
            .ToListAsync();
        if (publisherListingIds.Count == 0) return false;
        return await _ctx.Consultas
            .AnyAsync(c => c.UserId == userId && publisherListingIds.Contains(c.PropertyListingId));
    }

    public async Task<AgentRating?> GetByUserAndPublisherAsync(int publisherId, string userId) =>
        await _ctx.AgentRatings.FirstOrDefaultAsync(r => r.PublisherId == publisherId && r.UserId == userId);

    public async Task AddOrUpdateAsync(AgentRating rating)
    {
        var existing = await _ctx.AgentRatings
            .FirstOrDefaultAsync(r => r.UserId == rating.UserId && r.PublisherId == rating.PublisherId);
        if (existing is null)
            _ctx.AgentRatings.Add(rating);
        else
        {
            existing.PuntajeAtencion        = rating.PuntajeAtencion;
            existing.PuntajeRapidez         = rating.PuntajeRapidez;
            existing.PuntajeTransparencia   = rating.PuntajeTransparencia;
            existing.PuntajeProfesionalismo = rating.PuntajeProfesionalismo;
            existing.Comentario             = rating.Comentario;
            existing.FechaValoracion        = rating.FechaValoracion;
        }
        await _ctx.SaveChangesAsync();
    }

    public async Task<AgentRatingStatsDto> GetStatsAsync(int publisherId)
    {
        var ratings = await _ctx.AgentRatings.Where(r => r.PublisherId == publisherId).ToListAsync();
        if (ratings.Count == 0) return new AgentRatingStatsDto(0, 0, 0, 0, 0, 0);
        var a = ratings.Average(r => (double)r.PuntajeAtencion);
        var r2 = ratings.Average(r => (double)r.PuntajeRapidez);
        var t = ratings.Average(r => (double)r.PuntajeTransparencia);
        var p = ratings.Average(r => (double)r.PuntajeProfesionalismo);
        return new AgentRatingStatsDto(Math.Round(a,2), Math.Round(r2,2), Math.Round(t,2), Math.Round(p,2), Math.Round((a+r2+t+p)/4,2), ratings.Count);
    }

    public async Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad, int top = 20)
    {
        // Step 1: publishers with at least one rating
        var publisherIdsWithRatings = await _ctx.AgentRatings
            .Select(r => r.PublisherId).Distinct().ToListAsync();
        if (publisherIdsWithRatings.Count == 0) return [];

        IList<int> publisherIds = publisherIdsWithRatings;

        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            var pubIdsInCity = await _ctx.PropertyListings
                .Where(l => l.Location.Ciudad == ciudad && publisherIdsWithRatings.Contains(l.PublisherId))
                .Select(l => l.PublisherId).Distinct().ToListAsync();
            publisherIds = pubIdsInCity;
        }

        if (publisherIds.Count == 0) return [];

        // Step 2: Materialize publishers with ratings, listings, user
        var publishers = await _ctx.Publishers
            .Where(p => publisherIds.Contains(p.Id))
            .Include(p => p.Ratings)
            .Include(p => p.Listings)
            .Include(p => p.User)
            .ToListAsync();

        // Step 3: listing IDs per publisher for response time calc
        var listingEntries = await _ctx.PropertyListings
            .Where(l => publisherIds.Contains(l.PublisherId))
            .Select(l => new { l.Id, l.PublisherId })
            .ToListAsync();

        var listingToPublisher = listingEntries.ToDictionary(l => l.Id, l => l.PublisherId);
        var allListingIds = listingEntries.Select(l => l.Id).ToList();

        // Step 4: consulta IDs for those listings
        var consultaEntries = await _ctx.Consultas
            .Where(c => allListingIds.Contains(c.PropertyListingId))
            .Select(c => new { c.Id, c.PropertyListingId })
            .ToListAsync();

        var allConsultaIds = consultaEntries.Select(c => c.Id).ToList();

        // Step 5: messages in those consultas
        List<(int ConsultaId, bool EsDelPublisher, DateTime FechaEnvio)> messages = [];
        if (allConsultaIds.Count > 0)
        {
            messages = (await _ctx.ConsultaMensajes
                .Where(m => allConsultaIds.Contains(m.ConsultaId))
                .OrderBy(m => m.ConsultaId).ThenBy(m => m.FechaEnvio)
                .Select(m => new { m.ConsultaId, m.EsDelPublisher, m.FechaEnvio })
                .ToListAsync())
                .Select(m => (m.ConsultaId, m.EsDelPublisher, m.FechaEnvio))
                .ToList();
        }

        // Step 6: avg response time per publisher (hours)
        var responseTimesByPublisher = new Dictionary<int, List<double>>();
        var messagesByConsulta = messages.GroupBy(m => m.ConsultaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (consultaId, msgs) in messagesByConsulta)
        {
            var consultaEntry = consultaEntries.FirstOrDefault(c => c.Id == consultaId);
            if (consultaEntry is null) continue;
            if (!listingToPublisher.TryGetValue(consultaEntry.PropertyListingId, out var pubId)) continue;
            if (!responseTimesByPublisher.ContainsKey(pubId)) responseTimesByPublisher[pubId] = [];
            for (int i = 0; i < msgs.Count - 1; i++)
            {
                if (!msgs[i].EsDelPublisher && msgs[i + 1].EsDelPublisher)
                    responseTimesByPublisher[pubId].Add((msgs[i + 1].FechaEnvio - msgs[i].FechaEnvio).TotalHours);
            }
        }

        // Step 7: calculate scores and rank
        var now = DateTime.UtcNow;
        return publishers.Select(p =>
        {
            var rtgs = p.Ratings.ToList();
            var ratingAvg = rtgs.Count > 0
                ? rtgs.Average(r => (r.PuntajeAtencion + r.PuntajeRapidez + r.PuntajeTransparencia + r.PuntajeProfesionalismo) / 4.0)
                : 1.0;
            var ratingScore = (ratingAvg - 1) / 4.0 * 100;

            var avgHours = responseTimesByPublisher.TryGetValue(p.Id, out var times) && times.Count > 0
                ? times.Average() : 72.0;
            var responseScore = Math.Max(0, (72 - avgHours) / 72.0 * 100);

            var operaciones = p.Listings.Count(l => l.Estado == EstadoPublicacion.Vendida || l.Estado == EstadoPublicacion.Alquilada);
            var operacionesScore = Math.Min(100, operaciones / 50.0 * 100);

            var anios = p.User is not null ? (now - p.User.FechaRegistro).TotalDays / 365.25 : 0;
            var antiguedadScore = Math.Min(100, anios / 5.0 * 100);

            var rankingScore = 0.40 * ratingScore + 0.30 * responseScore + 0.20 * operacionesScore + 0.10 * antiguedadScore;

            var tiempoResp = responseTimesByPublisher.ContainsKey(p.Id) && responseTimesByPublisher[p.Id].Count > 0
                ? responseTimesByPublisher[p.Id].Average() : 0;

            return new AgentRankingItemDto(p.Id, p.Nombre, p.Tipo.ToString(), p.LogoUrl,
                Math.Round(rankingScore, 2), Math.Round(ratingAvg, 2), Math.Round(tiempoResp, 2),
                operaciones, Math.Round(anios, 1));
        })
        .OrderByDescending(x => x.RankingScore)
        .Take(top)
        .ToList();
    }
}
