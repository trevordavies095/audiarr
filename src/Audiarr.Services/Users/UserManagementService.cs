using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Audiarr.Core.DTOs;
using Audiarr.Core.Entities;
using Audiarr.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Audiarr.Services.Users;

public interface IUserManagementService
{
    Task<PaginatedResponse<UserListDto>> GetUsersAsync(UserListRequest request);
    Task<UserListDto?> GetUserByIdAsync(string userId);
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request);
    Task<bool> IsUsernameUniqueAsync(string username, string? excludeUserId = null);
    Task<bool> IsEmailUniqueAsync(string email, string? excludeUserId = null);
    Task<ResetPasswordResponse> ResetPasswordAsync(string targetUserId, string performedByUserId, ResetPasswordRequest request);
}

public class UserManagementService : IUserManagementService
{
    private readonly AudiarrContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserManagementService> _logger;
    private const string UserListCacheKeyPrefix = "userlist_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public UserManagementService(
        AudiarrContext context,
        IMemoryCache cache,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PaginatedResponse<UserListDto>> GetUsersAsync(UserListRequest request)
    {
        var cacheKey = $"{UserListCacheKeyPrefix}{request.PageNumber}_{request.PageSize}_{request.SortBy}_{request.SortOrder}_{request.SearchTerm}";
        
        if (_cache.TryGetValue<PaginatedResponse<UserListDto>>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Returning cached user list for key: {CacheKey}", cacheKey);
            return cachedResult!;
        }

        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u => 
                u.Username.ToLower().Contains(searchTerm) || 
                u.Email.ToLower().Contains(searchTerm));
        }

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, request.SortBy?.ToLower(), request.SortOrder?.ToLower());

        var skip = (request.PageNumber - 1) * request.PageSize;
        var users = await query
            .Skip(skip)
            .Take(request.PageSize)
            .Select(u => new UserListDto(
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.LastLogin,
                u.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        
        var response = new PaginatedResponse<UserListDto>(
            users,
            totalCount,
            request.PageNumber,
            request.PageSize,
            totalPages
        );

        _cache.Set(cacheKey, response, _cacheExpiration);
        _logger.LogDebug("Cached user list with key: {CacheKey}", cacheKey);

        return response;
    }

    public async Task<UserListDto?> GetUserByIdAsync(string userId)
    {
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserListDto(
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.LastLogin,
                u.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return user;
    }

    private IQueryable<User> ApplySorting(IQueryable<User> query, string? sortBy, string? sortOrder)
    {
        Expression<Func<User, object>> sortExpression = sortBy switch
        {
            "email" => u => u.Email,
            "lastlogin" => u => u.LastLogin ?? DateTime.MinValue,
            "createdat" => u => u.CreatedAt,
            _ => u => u.Username
        };

        return sortOrder == "desc" 
            ? query.OrderByDescending(sortExpression) 
            : query.OrderBy(sortExpression);
    }

    public void InvalidateCache()
    {
        _logger.LogDebug("Invalidating user list cache");
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request)
    {
        _logger.LogInformation("Creating new user: {Username}", request.Username);

        // Check for duplicate username
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
        {
            throw new InvalidOperationException($"Username '{request.Username}' already exists");
        }

        // Check for duplicate email
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            throw new InvalidOperationException($"Email '{request.Email}' is already registered");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role.ToLower(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Invalidate cache since we added a new user
        InvalidateCache();

        _logger.LogInformation("Successfully created user: {Username} with ID: {UserId}", user.Username, user.Id);

        return new CreateUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.CreatedAt
        );
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, string? excludeUserId = null)
    {
        var query = _context.Users.Where(u => u.Username.ToLower() == username.ToLower());
        
        if (!string.IsNullOrEmpty(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> IsEmailUniqueAsync(string email, string? excludeUserId = null)
    {
        var query = _context.Users.Where(u => u.Email.ToLower() == email.ToLower());
        
        if (!string.IsNullOrEmpty(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return !await query.AnyAsync();
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(string targetUserId, string performedByUserId, ResetPasswordRequest request)
    {
        _logger.LogInformation("Resetting password for user {TargetUserId} by admin {AdminUserId}", targetUserId, performedByUserId);

        // Check if target user exists
        var targetUser = await _context.Users.FindAsync(targetUserId);
        if (targetUser == null)
        {
            throw new InvalidOperationException($"User with ID '{targetUserId}' not found");
        }

        // Prevent admin from resetting their own password through this method
        if (targetUserId == performedByUserId)
        {
            throw new InvalidOperationException("Admins cannot reset their own password through this method. Please use the password change feature.");
        }

        string newPassword;
        string method;

        if (request.GenerateRandom)
        {
            // Generate a secure random password
            newPassword = GenerateSecurePassword();
            method = "generated";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ManualPassword))
            {
                throw new ArgumentException("Manual password cannot be empty when not generating a random password");
            }

            if (request.ManualPassword.Length < 8)
            {
                throw new ArgumentException("Password must be at least 8 characters long");
            }

            newPassword = request.ManualPassword;
            method = "manual";
        }

        // Hash the new password
        targetUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        targetUser.UpdatedAt = DateTime.UtcNow;

        // Invalidate all existing sessions for the user
        var userSessions = await _context.Sessions
            .Where(s => s.UserId == targetUserId)
            .ToListAsync();

        if (userSessions.Any())
        {
            _context.Sessions.RemoveRange(userSessions);
            _logger.LogInformation("Invalidated {SessionCount} sessions for user {UserId}", userSessions.Count, targetUserId);
        }

        // Create audit log entry
        var auditLog = new AuditLog
        {
            Action = "PasswordReset",
            TargetUserId = targetUserId,
            PerformedByUserId = performedByUserId,
            Details = $"Password reset using {method} method",
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);

        // Save all changes
        await _context.SaveChangesAsync();

        // Invalidate cache since user data has changed
        InvalidateCache();

        _logger.LogInformation("Successfully reset password for user {TargetUserId} using {Method} method", targetUserId, method);

        return new ResetPasswordResponse(newPassword, method);
    }

    private static string GenerateSecurePassword(int length = 12)
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        const string allChars = upperCase + lowerCase + digits + special;

        var password = new char[length];
        var randomBytes = new byte[length * 4];
        
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Ensure at least one character from each category
        password[0] = upperCase[Math.Abs(BitConverter.ToInt32(randomBytes, 0)) % upperCase.Length];
        password[1] = lowerCase[Math.Abs(BitConverter.ToInt32(randomBytes, 4)) % lowerCase.Length];
        password[2] = digits[Math.Abs(BitConverter.ToInt32(randomBytes, 8)) % digits.Length];
        password[3] = special[Math.Abs(BitConverter.ToInt32(randomBytes, 12)) % special.Length];

        // Fill the rest with random characters from all categories
        for (int i = 4; i < length; i++)
        {
            password[i] = allChars[Math.Abs(BitConverter.ToInt32(randomBytes, i * 4) % allChars.Length)];
        }

        // Shuffle the password to avoid predictable patterns
        for (int i = length - 1; i > 0; i--)
        {
            int j = Math.Abs(BitConverter.ToInt32(randomBytes, i * 4) % (i + 1));
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}