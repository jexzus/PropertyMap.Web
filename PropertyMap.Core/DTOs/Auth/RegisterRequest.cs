using System.ComponentModel.DataAnnotations;

namespace PropertyMap.Core.DTOs.Auth;

public record RegisterRequest(
    [StringLength(100)] string Nombre,
    [StringLength(100)] string Apellido,
    string Email,
    string Password,
    string ConfirmPassword
);
