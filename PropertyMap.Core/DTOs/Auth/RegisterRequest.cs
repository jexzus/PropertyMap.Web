using System.ComponentModel.DataAnnotations;

namespace PropertyMap.Core.DTOs.Auth;

public record RegisterRequest(
    [StringLength(100)] string Nombre,
    [StringLength(100)] string Apellido,
    [StringLength(256)] string Email,
    [StringLength(128, MinimumLength = 8)] string Password,
    [StringLength(128, MinimumLength = 8)] string ConfirmPassword
);
