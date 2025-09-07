namespace Audiarr.Core.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RefreshTokenRequest(string RefreshToken);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

public record UserDto(
    string Id,
    string Username,
    string Email,
    string Role,
    DateTime? LastLogin
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);