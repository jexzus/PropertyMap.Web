// PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ConsultaRepository : IConsultaRepository
{
    private readonly AppDbContext _ctx;

    public ConsultaRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<Consulta> GetOrCreateAsync(int listingId, string userId)
    {
        var existing = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstOrDefaultAsync(c => c.PropertyListingId == listingId && c.UserId == userId);

        if (existing is not null) return existing;

        var consulta = new Consulta
        {
            PropertyListingId = listingId,
            UserId = userId,
            FechaCreacion = DateTime.UtcNow,
            FechaUltimoMensaje = DateTime.UtcNow
        };
        _ctx.Consultas.Add(consulta);
        await _ctx.SaveChangesAsync();

        return await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstAsync(c => c.Id == consulta.Id);
    }

    public async Task<ConsultaDetailDto?> GetByIdAsync(int consultaId, string requesterId)
    {
        var consulta = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .Include(c => c.Mensajes).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta is null) return null;

        var isOwner = consulta.UserId == requesterId;
        var isPublisher = consulta.PropertyListing.Publisher?.UserId == requesterId;
        if (!isOwner && !isPublisher) return null;

        return new ConsultaDetailDto(
            consulta.Id,
            consulta.PropertyListingId,
            consulta.PropertyListing.Titulo,
            consulta.PropertyListing.Operacion.ToString(),
            consulta.PropertyListing.Publisher?.Id,
            consulta.Mensajes
                .OrderBy(m => m.FechaEnvio)
                .Select(m => new ConsultaMensajeDto(
                    m.Id,
                    $"{m.Sender.Nombre} {m.Sender.Apellido}",
                    m.EsDelPublisher,
                    m.Mensaje,
                    m.FechaEnvio))
                .ToList());
    }

    public async Task<List<ConsultaSummaryDto>> GetByUserAsync(string userId)
    {
        return await _ctx.Consultas
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.FechaUltimoMensaje)
            .Select(c => new ConsultaSummaryDto(
                c.Id,
                c.PropertyListingId,
                c.PropertyListing.Titulo,
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.Mensaje).FirstOrDefault() ?? "",
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.EsDelPublisher).FirstOrDefault(),
                c.FechaUltimoMensaje))
            .ToListAsync();
    }

    public async Task<List<ConsultaSummaryDto>> GetByPublisherAsync(string publisherUserId)
    {
        var listingIds = await _ctx.PropertyListings
            .Where(l => l.Publisher != null && l.Publisher.UserId == publisherUserId)
            .Select(l => l.Id)
            .ToListAsync();

        if (listingIds.Count == 0) return [];

        return await _ctx.Consultas
            .Where(c => listingIds.Contains(c.PropertyListingId))
            .OrderByDescending(c => c.FechaUltimoMensaje)
            .Select(c => new ConsultaSummaryDto(
                c.Id,
                c.PropertyListingId,
                c.PropertyListing.Titulo,
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.Mensaje).FirstOrDefault() ?? "",
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.EsDelPublisher).FirstOrDefault(),
                c.FechaUltimoMensaje))
            .ToListAsync();
    }

    public async Task<ConsultaMensajeDto> AddMessageAsync(ConsultaMensaje message)
    {
        var consulta = await _ctx.Consultas.FindAsync(message.ConsultaId)
            ?? throw new InvalidOperationException($"Consulta {message.ConsultaId} not found.");
        consulta.FechaUltimoMensaje = message.FechaEnvio;
        _ctx.ConsultaMensajes.Add(message);
        await _ctx.SaveChangesAsync();

        await _ctx.Entry(message).Reference(m => m.Sender).LoadAsync();
        return new ConsultaMensajeDto(
            message.Id,
            $"{message.Sender.Nombre} {message.Sender.Apellido}",
            message.EsDelPublisher,
            message.Mensaje,
            message.FechaEnvio);
    }

    public async Task<bool> CanPublisherReplyAsync(int consultaId, string publisherUserId)
    {
        var consulta = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstOrDefaultAsync(c => c.Id == consultaId);
        return consulta?.PropertyListing.Publisher?.UserId == publisherUserId;
    }

    public async Task<string?> GetConsultaOwnerUserIdAsync(int consultaId)
    {
        return await _ctx.Consultas
            .Where(c => c.Id == consultaId)
            .Select(c => c.UserId)
            .FirstOrDefaultAsync();
    }

    public async Task CreateNotificationAsync(Notification notification)
    {
        _ctx.Notifications.Add(notification);
        await _ctx.SaveChangesAsync();
    }
}
