using Audiarr.Core.DTOs;
using Audiarr.Services.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Audiarr.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/users")
            .WithTags("Users")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Get paginated list of users")
            .WithDescription("Retrieves a paginated list of users with optional sorting and search. Requires admin role.")
            .Produces<PaginatedResponse<UserListDto>>(200)
            .Produces(401)
            .Produces(403);

        group.MapGet("/{id}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Retrieves a specific user by their ID. Requires admin role.")
            .Produces<UserListDto>(200)
            .Produces(404)
            .Produces(401)
            .Produces(403);

        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create a new user")
            .WithDescription("Creates a new user account. Requires admin role.")
            .Produces<CreateUserResponse>(201)
            .Produces<ProblemDetails>(400)
            .Produces(401)
            .Produces(403);

        group.MapGet("/check-username/{username}", CheckUsernameAvailability)
            .WithName("CheckUsernameAvailability")
            .WithSummary("Check if username is available")
            .WithDescription("Checks if a username is unique and available for use.")
            .Produces<bool>(200);

        group.MapGet("/check-email/{email}", CheckEmailAvailability)
            .WithName("CheckEmailAvailability")
            .WithSummary("Check if email is available")
            .WithDescription("Checks if an email address is unique and available for use.")
            .Produces<bool>(200);

        group.MapPost("/{userId}/reset-password", ResetUserPassword)
            .WithName("ResetUserPassword")
            .WithSummary("Reset a user's password")
            .WithDescription("Allows admins to reset passwords for other users. Cannot reset own password.")
            .Produces<ResetPasswordResponse>(200)
            .Produces<ProblemDetails>(400)
            .Produces(401)
            .Produces(403);

        group.MapPut("/{userId}/status", UpdateUserStatus)
            .WithName("UpdateUserStatus")
            .WithSummary("Enable or disable a user account")
            .WithDescription("Updates a user's active status. Admins cannot disable their own account or the last admin account.")
            .Produces<UserStatusResponse>(200)
            .Produces<ProblemDetails>(400)
            .Produces(404)
            .Produces(401)
            .Produces(403);

        group.MapDelete("/{userId}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Delete a user")
            .WithDescription("Permanently deletes a user account. Admins cannot delete their own account.")
            .Produces(204)
            .Produces<ProblemDetails>(400)
            .Produces(404)
            .Produces(401)
            .Produces(403);

        return app;
    }

    private static async Task<Results<Ok<PaginatedResponse<UserListDto>>, BadRequest<string>>> GetUsers(
        IUserManagementService userService,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = "username",
        string? sortOrder = "asc",
        string? searchTerm = null)
    {
        if (pageNumber < 1)
            return TypedResults.BadRequest("Page number must be greater than 0");

        if (pageSize < 1 || pageSize > 100)
            return TypedResults.BadRequest("Page size must be between 1 and 100");

        var validSortFields = new[] { "username", "email", "lastlogin", "createdat" };
        if (sortBy != null && !validSortFields.Contains(sortBy.ToLower()))
            return TypedResults.BadRequest($"Invalid sort field. Valid fields are: {string.Join(", ", validSortFields)}");

        var validSortOrders = new[] { "asc", "desc" };
        if (sortOrder != null && !validSortOrders.Contains(sortOrder.ToLower()))
            return TypedResults.BadRequest("Sort order must be 'asc' or 'desc'");

        var request = new UserListRequest(pageNumber, pageSize, sortBy, sortOrder, searchTerm);
        var result = await userService.GetUsersAsync(request);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<UserListDto>, NotFound>> GetUserById(
        IUserManagementService userService,
        string id)
    {
        var user = await userService.GetUserByIdAsync(id);

        return user != null
            ? TypedResults.Ok(user)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Created<CreateUserResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> CreateUser(
        IUserManagementService userService,
        [FromBody] CreateUserRequest request,
        ILogger<CreateUserRequest> logger)
    {
        // Validate username format (3-20 characters, alphanumeric + underscore)
        if (string.IsNullOrWhiteSpace(request.Username) ||
            request.Username.Length < 3 ||
            request.Username.Length > 20 ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]+$"))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Username",
                Detail = "Username must be 3-20 characters and contain only letters, numbers, and underscores"
            });
        }

        // Validate email format
        if (string.IsNullOrWhiteSpace(request.Email) ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Email",
                Detail = "Please provide a valid email address"
            });
        }

        // Validate password complexity
        if (string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit) ||
            !request.Password.Any(c => !char.IsLetterOrDigit(c)))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Password",
                Detail = "Password must be at least 8 characters with uppercase, lowercase, number, and special character"
            });
        }

        // Validate role
        var validRoles = new[] { "user", "admin" };
        if (!validRoles.Contains(request.Role.ToLower()))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Role",
                Detail = "Role must be either 'user' or 'admin'"
            });
        }

        try
        {
            var result = await userService.CreateUserAsync(request);
            return TypedResults.Created($"/api/v2/users/{result.Id}", result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to create user: {Message}", ex.Message);
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "User Creation Failed",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating user");
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "User Creation Failed",
                Detail = "An unexpected error occurred while creating the user"
            });
        }
    }

    private static async Task<Ok<bool>> CheckUsernameAvailability(
        IUserManagementService userService,
        string username)
    {
        var isAvailable = await userService.IsUsernameUniqueAsync(username);
        return TypedResults.Ok(isAvailable);
    }

    private static async Task<Ok<bool>> CheckEmailAvailability(
        IUserManagementService userService,
        string email)
    {
        var isAvailable = await userService.IsEmailUniqueAsync(email);
        return TypedResults.Ok(isAvailable);
    }

    private static async Task<Results<Ok<ResetPasswordResponse>, ProblemHttpResult>> ResetUserPassword(
        IUserManagementService userService,
        HttpContext httpContext,
        string userId,
        ResetPasswordRequest request)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
        {
            return TypedResults.Problem(
                detail: "Admin user ID not found in token",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        try
        {
            var result = await userService.ResetPasswordAsync(userId, adminUserId, request);
            return TypedResults.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    }

    private static async Task<Results<Ok<UserStatusResponse>, ProblemHttpResult, NotFound>> UpdateUserStatus(
        IUserManagementService userService,
        HttpContext httpContext,
        string userId,
        UserStatusRequest request,
        ILogger<IUserManagementService> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
        {
            return TypedResults.Problem(
                detail: "Admin user ID not found in token",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        try
        {
            var result = await userService.UpdateUserStatusAsync(userId, adminUserId, request);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to update user status for {UserId}: {Message}", userId, ex.Message);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating user status for {UserId}", userId);
            return TypedResults.Problem(
                detail: "An unexpected error occurred",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult, NotFound>> DeleteUser(
        IUserManagementService userService,
        HttpContext httpContext,
        string userId,
        ILogger<IUserManagementService> logger)
    {
        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminUserId))
        {
            return TypedResults.Problem(
                detail: "Admin user ID not found in token",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        try
        {
            await userService.DeleteUserAsync(userId, adminUserId);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to delete user {UserId}: {Message}", userId, ex.Message);
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }
}