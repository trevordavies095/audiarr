namespace Audiarr.Core.DTOs;

public record UserListDto(
    string Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime? LastLogin,
    DateTime CreatedAt
);

public record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);

public record UserListRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = "username",
    string? SortOrder = "asc",
    string? SearchTerm = null
);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string Role = "user"
);

public record CreateUserResponse(
    string Id,
    string Username,
    string Email,
    string Role,
    DateTime CreatedAt
);

public record ResetPasswordRequest(
    bool GenerateRandom = true,
    string? ManualPassword = null
);

public record ResetPasswordResponse(
    string NewPassword,
    string Method // "generated" or "manual"
);