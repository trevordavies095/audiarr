using Audiarr.Api.Models.DTOs;
using Audiarr.Api.Models.Entities;

namespace Audiarr.Api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string username, string password);
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
    Task<bool> RevokeAllUserSessionsAsync(string userId);
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<User?> ValidateUserAsync(string username, string password);
}