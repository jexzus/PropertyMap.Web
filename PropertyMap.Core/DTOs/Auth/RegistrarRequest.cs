namespace PropertyMap.Core.DTOs.Auth;
public record RegistrarRequest(string Email, string Nombre, string Apellido, string Password, string ConfirmPassword);
