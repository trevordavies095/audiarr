using System.Linq.Expressions;
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
}