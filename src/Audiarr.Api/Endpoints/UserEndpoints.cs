using Audiarr.Core.DTOs;
using Audiarr.Services.Users;
using Microsoft.AspNetCore.Http.HttpResults;

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
}