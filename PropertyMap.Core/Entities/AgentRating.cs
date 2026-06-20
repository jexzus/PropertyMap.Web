namespace PropertyMap.Core.Entities;

public class AgentRating
{
    public int Id { get; set; }
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PuntajeAtencion { get; set; }
    public int PuntajeRapidez { get; set; }
    public int PuntajeTransparencia { get; set; }
    public int PuntajeProfesionalismo { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; } = DateTime.UtcNow;
}
