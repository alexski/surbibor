using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Surbibor.Api.Auth;
using Surbibor.Infrastructure;

namespace Surbibor.Api.Hubs;

[Authorize]
public class GameHub(SurbiborDbContext db) : Hub
{
    public static string GroupName(Guid gameId) => $"game-{gameId}";

    public async Task JoinGame(Guid gameId)
    {
        var userId = Context.User!.GetUserId();

        var isMember = await db.GameMemberships.AnyAsync(m => m.GameId == gameId && m.UserId == userId);
        if (!isMember)
        {
            throw new HubException("You are not a member of this game.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public async Task LeaveGame(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));
    }
}
