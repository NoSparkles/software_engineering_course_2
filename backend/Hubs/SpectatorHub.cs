using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Services;
using Models.InMemoryModels;
using Extensions;
using Games;

namespace Hubs
{
    public class SpectatorHub : Hub
    {
        private readonly RoomService RoomService;
        // Map connectionId -> (roomKey, spectatorId) 
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string roomKey, string spectatorId)> ConnectionRoomMap =
            new System.Collections.Concurrent.ConcurrentDictionary<string, (string roomKey, string spectatorId)>();

        public SpectatorHub(RoomService roomService)
        {
            RoomService = roomService;
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            if (ConnectionRoomMap.TryRemove(connectionId, out var mapping))
            {
                var roomKey = mapping.roomKey;
                var spectatorId = mapping.spectatorId;
                if (RoomService.Rooms.TryGetValue(roomKey, out var room))
                {
                    lock (room.RoomSpectators)
                    {
                        var spec = room.RoomSpectators.FirstOrDefault(s => s.PlayerId == spectatorId);
                        if (spec != null)
                        {
                            room.RoomSpectators.Remove(spec);
                        }
                    }

                    await Groups.RemoveFromGroupAsync(connectionId, roomKey);
                    await Clients.Group(roomKey).SendAsync("SpectatorLeft", spectatorId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinSpectate(string gameType, string roomCode, string spectatorId, string? username)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!RoomService.Rooms.TryGetValue(roomKey, out var room))
            {
                await Clients.Caller.SendAsync("JoinFailed", "Room does not exist");
                return;
            }

            Console.WriteLine($"SpectatorHub.JoinSpectate called - conn:{Context.ConnectionId} spectatorId:{spectatorId} room:{roomKey} username:{username}");

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            // register via RoomService to keep consistent state
            await RoomService.JoinAsSpectator(gameType, roomCode, spectatorId, null, Context.ConnectionId);

            ConnectionRoomMap[Context.ConnectionId] = (roomKey, spectatorId);

            // Send current game state to caller
            Console.WriteLine($"SpectatorHub: sending initial game state to {Context.ConnectionId} for room {roomKey}");
            var game = room.Game;
            switch (game)
            {
                case FourInARowGame four:
                    await Clients.Caller.SendAsync("GameStateUpdate", four.GetGameState());
                    Console.WriteLine("SpectatorHub: sent GameStateUpdate (FourInARow)");
                    break;
                case PairMatching pair:
                    await Clients.Caller.SendAsync("GameStateUpdate", pair.GetGameState());
                    Console.WriteLine("SpectatorHub: sent GameStateUpdate (PairMatching)");
                    break;
                case RockPaperScissors rps:
                    await Clients.Caller.SendAsync("GameStateUpdate", rps.GetGameStatePublic());
                    Console.WriteLine("SpectatorHub: sent GameStateUpdate (RPS)");
                    break;
            }

            await Clients.Group(roomKey).SendAsync("SpectatorJoined", spectatorId, username ?? "");
        }

        public async Task LeaveSpectate(string gameType, string roomCode, string spectatorId)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!RoomService.Rooms.TryGetValue(roomKey, out var room))
            {
                return;
            }

            lock (room.RoomSpectators)
            {
                var spec = room.RoomSpectators.FirstOrDefault(s => s.PlayerId == spectatorId);
                if (spec != null)
                {
                    room.RoomSpectators.Remove(spec);
                }
            }

            // remove mapping if present
            ConnectionRoomMap.TryRemove(Context.ConnectionId, out _);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            await Clients.Group(roomKey).SendAsync("SpectatorLeft", spectatorId);
        }

        // Called by game logic to push updates to spectators
        public async Task BroadcastToSpectators(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!RoomService.Rooms.TryGetValue(roomKey, out var room)) return;
            var game = room.Game;

            switch (game)
            {
                case FourInARowGame four:
                    await Clients.Group(roomKey).SendAsync("GameStateUpdate", four.GetGameState());
                    break;
                case PairMatching pair:
                    await Clients.Group(roomKey).SendAsync("GameStateUpdate", pair.GetGameState());
                    break;
                case RockPaperScissors rps:
                    await Clients.Group(roomKey).SendAsync("GameStateUpdate", rps.GetGameStatePublic());
                    break;
            }
        }
    }
}
