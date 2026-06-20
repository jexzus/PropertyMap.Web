namespace PropertyMap.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public string? Detalles { get; set; }
    public DateTime FechaAccion { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
