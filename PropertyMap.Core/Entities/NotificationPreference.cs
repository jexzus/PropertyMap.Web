namespace PropertyMap.Core.Entities;

public class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public bool RecibirEmail { get; set; } = true;
    public bool RecibirPush { get; set; } = true;
    public bool NuevasConsultas { get; set; } = true;
    public bool NuevasRespuestas { get; set; } = true;
    public bool AlertasCoincidencia { get; set; } = true;
}
