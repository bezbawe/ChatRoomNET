using System.Security.Claims;
using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Services;

namespace ChatRoomNET.Web.Endpoints;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/rooms").RequireAuthorization();

        group.MapPost("/", async (CreateRoomRequest request, ClaimsPrincipal user, IRoomService rooms) =>
        {
            var room = await rooms.CreateAsync(GetUserId(user), request);
            return Results.Created($"/api/rooms/{room.Id}", room);
        });

        group.MapGet("/", async (ClaimsPrincipal user, IRoomService rooms) =>
            Results.Ok(await rooms.GetMyRoomsAsync(GetUserId(user))));

        group.MapPost("/join", async (JoinRoomRequest request, ClaimsPrincipal user, IRoomService rooms) =>
        {
            var room = await rooms.JoinAsync(GetUserId(user), request);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        group.MapGet("/{id:guid}/members", async (Guid id, ClaimsPrincipal user, IRoomService rooms) =>
        {
            var members = await rooms.GetMembersAsync(id, GetUserId(user));
            return members is null ? Results.NotFound() : Results.Ok(members);
        });

        group.MapGet("/{id:guid}/messages", async (
            Guid id, long? before, int? take, ClaimsPrincipal user, IMessageService messages) =>
        {
            var history = await messages.GetHistoryAsync(id, GetUserId(user), before, take ?? MessageService.DefaultTake);
            return history is null ? Results.NotFound() : Results.Ok(history);
        });
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
