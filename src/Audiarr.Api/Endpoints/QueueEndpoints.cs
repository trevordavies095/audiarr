using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Audiarr.Core.DTOs;
using Audiarr.Core.DTOs.Requests;
using Audiarr.Core.Services;
using System.Security.Claims;

namespace Audiarr.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/queue")
            .RequireAuthorization()
            .WithTags("Queue");

        // GET /api/v2/queue - Get current queue state
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var queue = await queueService.GetQueueAsync(userId);
            return Results.Ok(queue);
        })
        .WithName("GetQueue")
        .WithSummary("Get current queue state")
        .WithDescription("Returns the current playback queue for the authenticated user")
        .Produces<QueueStateDto>()
        .Produces(401);

        // POST /api/v2/queue/tracks - Add tracks to queue
        group.MapPost("/tracks", async (
            [FromBody] AddToQueueRequest request,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.AddTracksAsync(userId, request);
                return Results.Ok(queue);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("AddTracksToQueue")
        .WithSummary("Add tracks to queue")
        .WithDescription("Adds one or more tracks to the playback queue")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // DELETE /api/v2/queue/tracks/{index} - Remove track at index
        group.MapDelete("/tracks/{index:int}", async (
            int index,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.RemoveTrackAtIndexAsync(userId, index);
                return Results.Ok(queue);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid index",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("RemoveTrackFromQueue")
        .WithSummary("Remove track at index")
        .WithDescription("Removes the track at the specified index from the queue")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // DELETE /api/v2/queue/clear - Clear entire queue
        group.MapDelete("/clear", async (
            [AsParameters] ClearQueueQuery query,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var queue = await queueService.ClearQueueAsync(userId, query.KeepCurrentTrack);
            return Results.Ok(queue);
        })
        .WithName("ClearQueue")
        .WithSummary("Clear entire queue")
        .WithDescription("Clears all tracks from the playback queue")
        .Produces<QueueStateDto>()
        .Produces(401);

        // PUT /api/v2/queue/reorder - Move track in queue
        group.MapPut("/reorder", async (
            [FromBody] ReorderQueueRequest request,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.ReorderQueueAsync(userId, request);
                return Results.Ok(queue);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid index",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("ReorderQueue")
        .WithSummary("Move track in queue")
        .WithDescription("Moves a track to a new position in the queue")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // Additional endpoints for completeness

        // PUT /api/v2/queue - Update queue settings
        group.MapPut("/", async (
            [FromBody] UpdateQueueRequest request,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.UpdateQueueSettingsAsync(userId, request);
                return Results.Ok(queue);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("UpdateQueueSettings")
        .WithSummary("Update queue settings")
        .WithDescription("Updates queue settings like repeat mode, shuffle, or current index")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // POST /api/v2/queue/replace - Replace entire queue
        group.MapPost("/replace", async (
            [FromBody] ReplaceQueueRequest request,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.ReplaceQueueAsync(userId, request);
                return Results.Ok(queue);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("ReplaceQueue")
        .WithSummary("Replace entire queue")
        .WithDescription("Replaces the entire queue with a new set of tracks")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // Playback Control Endpoints

        // PUT /api/v2/queue/settings - Update queue settings (repeat/shuffle)
        group.MapPut("/settings", async (
            [FromBody] UpdateQueueRequest request,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.UpdateQueueSettingsAsync(userId, request);
                return Results.Ok(queue);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("UpdatePlaybackSettings")
        .WithSummary("Update queue settings")
        .WithDescription("Updates repeat mode and shuffle settings for the playback queue")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces(401);

        // POST /api/v2/queue/next - Skip to next track
        group.MapPost("/next", async (
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.NextTrackAsync(userId);
                return Results.Ok(queue);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Queue empty",
                    Detail = ex.Message,
                    Status = 404
                });
            }
        })
        .WithName("NextTrack")
        .WithSummary("Skip to next track")
        .WithDescription("Moves to the next track in the queue, respecting repeat mode settings")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(404)
        .Produces(401);

        // POST /api/v2/queue/previous - Go to previous track
        group.MapPost("/previous", async (
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.PreviousTrackAsync(userId);
                return Results.Ok(queue);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Queue empty",
                    Detail = ex.Message,
                    Status = 404
                });
            }
        })
        .WithName("PreviousTrack")
        .WithSummary("Go to previous track")
        .WithDescription("Moves to the previous track in the queue, respecting repeat mode settings")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(404)
        .Produces(401);

        // PUT /api/v2/queue/position/{index} - Jump to specific track
        group.MapPut("/position/{index:int}", async (
            int index,
            ClaimsPrincipal user,
            IQueueService queueService) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var queue = await queueService.JumpToPositionAsync(userId, index);
                return Results.Ok(queue);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new ProblemDetails
                {
                    Title = "Queue empty",
                    Detail = ex.Message,
                    Status = 404
                });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid index",
                    Detail = ex.Message,
                    Status = 400
                });
            }
        })
        .WithName("JumpToPosition")
        .WithSummary("Jump to specific track")
        .WithDescription("Jumps to a specific track position in the queue")
        .Produces<QueueStateDto>()
        .Produces<ProblemDetails>(400)
        .Produces<ProblemDetails>(404)
        .Produces(401);
    }
}

// Query parameter class for clear endpoint
public record ClearQueueQuery
{
    public bool KeepCurrentTrack { get; init; } = false;
}