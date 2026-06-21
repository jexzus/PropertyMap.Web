namespace PropertyMap.Core.DTOs.Auth;

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword
);
