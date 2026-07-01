namespace PropertyMap.Core.Entities;

public class PreRegistroToken
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool Usado { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Tipo { get; set; } = ""; // "registro" | "recuperacion"
}
