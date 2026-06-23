// PropertyMap.Core/Entities/ConsultaMensaje.cs
namespace PropertyMap.Core.Entities;

public class ConsultaMensaje
{
    public int Id { get; set; }
    public int ConsultaId { get; set; }
    public string SenderId { get; set; } = "";
    public bool EsDelPublisher { get; set; }
    public string Mensaje { get; set; } = "";
    public DateTime FechaEnvio { get; set; }

    public Consulta Consulta { get; set; } = null!;
    public ApplicationUser Sender { get; set; } = null!;
}
