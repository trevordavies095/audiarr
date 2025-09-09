using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Audiarr.Data.Context;
using Audiarr.Core.Configuration;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;
using Audiarr.Core.Interfaces;

namespace Audiarr.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AudiarrContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AudiarrContext context,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var user = await ValidateUserAsync(username, password);
        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", username);
            return null;
        }

        // Update last login
        user.LastLogin = DateTime.UtcNow;
        _context.Users.Update(user);  // Mark the user entity as modified

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshToken);

        // Create session
        var session = new Session
        {
            UserId = user.Id,
            RefreshTokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();  // This will now save both the session and user changes

        _logger.LogInformation("User {Username} logged in successfully", username);

        return new LoginResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            new UserDto(user.Id, user.Username, user.Email, user.Role, user.LastLogin)
        );
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        var refreshTokenHash = HashToken(refreshToken);

        var session = await _context.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);

        if (session == null || !session.IsActive)
        {
            _logger.LogWarning("Invalid or expired refresh token attempted");
            return null;
        }

        // Rotate refresh token
        session.RevokedAt = DateTime.UtcNow;

        var newAccessToken = GenerateAccessToken(session.User);
        var newRefreshToken = GenerateRefreshToken();
        var newRefreshTokenHash = HashToken(newRefreshToken);

        // Create new session
        var newSession = new Session
        {
            UserId = session.UserId,
            RefreshTokenHash = newRefreshTokenHash,
            DeviceName = session.DeviceName,
            DeviceType = session.DeviceType,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        };

        _context.Sessions.Add(newSession);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tokens refreshed for user {UserId}", session.UserId);

        return new TokenResponse(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var refreshTokenHash = HashToken(refreshToken);

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);

        if (session == null)
        {
            return false;
        }

        session.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User session revoked for user {UserId}", session.UserId);

        return true;
    }

    public async Task<bool> RevokeAllUserSessionsAsync(string userId)
    {
        var sessions = await _context.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("All sessions revoked for user {UserId}", userId);

        return true;
    }

    public string GenerateAccessToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null || !user.IsActive)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}