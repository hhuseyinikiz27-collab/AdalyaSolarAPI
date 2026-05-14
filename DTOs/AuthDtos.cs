namespace AdalyaSolarAPI.DTOs;

public record LoginDto(string Email, string Password);
public record RegisterDto(string Name, string Email, string Password, string? Phone = null, string? ReferralCode = null);
public record UpdateProfileDto(string Name, string Phone);
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
public record GoogleLoginDto(string IdToken);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Token, string NewPassword);
