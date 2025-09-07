using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Audiarr.Data.Context;
using Audiarr.Core.DTOs;
using Audiarr.Core.Interfaces;

namespace Audiarr.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        // Login endpoint
        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            HttpContext httpContext) =>
        {
            var result = await authService.LoginAsync(request.Username, request.Password);
            
            if (result == null)
            {
                return Results.Unauthorized();
            }
            
            // Set device info from request headers
            if (httpContext.Request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                // This would be used to update the session with device info
                // For now, we'll just log it
            }
            
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithSummary("Authenticate user and get access token")
        .WithDescription("Authenticates a user with username and password, returns JWT access token and refresh token")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Refresh token endpoint
        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IAuthService authService) =>
        {
            var result = await authService.RefreshTokenAsync(request.RefreshToken);
            
            if (result == null)
            {
                return Results.Unauthorized();
            }
            
            return Results.Ok(result);
        })
        .WithName("RefreshToken")
        .WithSummary("Refresh access token")
        .WithDescription("Exchange a valid refresh token for a new access token and refresh token")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Logout endpoint
        group.MapPost("/logout", [Authorize] async (
            HttpContext httpContext,
            IAuthService authService) =>
        {
            // Get refresh token from request body or header
            string? refreshToken = null;
            
            if (httpContext.Request.HasJsonContentType())
            {
                var request = await httpContext.Request.ReadFromJsonAsync<RefreshTokenRequest>();
                refreshToken = request?.RefreshToken;
            }
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.BadRequest("Refresh token is required");
            }
            
            var result = await authService.LogoutAsync(refreshToken);
            
            if (!result)
            {
                return Results.BadRequest("Failed to logout");
            }
            
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Logout user")
        .WithDescription("Revokes the provided refresh token")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // Get current user endpoint
        group.MapGet("/me", [Authorize] async (
            ClaimsPrincipal user,
            AudiarrContext context) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            
            var dbUser = await context.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserDto(
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.LastLogin))
                .FirstOrDefaultAsync();
            
            if (dbUser == null)
            {
                return Results.NotFound();
            }
            
            return Results.Ok(dbUser);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Get current user info")
        .WithDescription("Returns information about the currently authenticated user")
        .RequireAuthorization()
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // Change password endpoint
        group.MapPost("/change-password", [Authorize] async (
            ChangePasswordRequest request,
            ClaimsPrincipal user,
            IAuthService authService,
            AudiarrContext context) =>
        {
            var username = user.FindFirst(ClaimTypes.Name)?.Value;
            
            if (string.IsNullOrEmpty(username))
            {
                return Results.Unauthorized();
            }
            
            // Validate current password
            var validUser = await authService.ValidateUserAsync(username, request.CurrentPassword);
            if (validUser == null)
            {
                return Results.BadRequest("Current password is incorrect");
            }
            
            // Update password
            validUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await context.SaveChangesAsync();
            
            // Revoke all sessions for security
            await authService.RevokeAllUserSessionsAsync(validUser.Id);
            
            return Results.Ok(new { message = "Password changed successfully. Please login again." });
        })
        .WithName("ChangePassword")
        .WithSummary("Change user password")
        .WithDescription("Changes the password for the current user and revokes all sessions")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}