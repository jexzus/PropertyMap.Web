namespace PropertyMap.Core.Entities;

public class PropertyAnswer
{
    public int Id { get; set; }
    public int PropertyQuestionId { get; set; }
    public PropertyQuestion PropertyQuestion { get; set; } = null!;
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public string Mensaje { get; set; } = "";
    public DateTime FechaRespuesta { get; set; } = DateTime.UtcNow;
}
