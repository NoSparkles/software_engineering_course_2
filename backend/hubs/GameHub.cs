using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Hubs
{
    public class GameHub : Hub
    {
        // Maps roomKey → playerId → connectionId
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> RoomUsers = new();

        public async Task JoinRoom(string gameType, string roomCode, string playerId)
        {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";
            Console.WriteLine($"Player {playerId} joining room {roomCode} for game {gameType}");

            RoomUsers.TryAdd(roomKey, new Dictionary<string, string>());
            var users = RoomUsers[roomKey];

            bool shouldNotifyStart;

            lock (users)
            {
                users[playerId] = Context.ConnectionId;
                shouldNotifyStart = users.Count == 2;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

            if (shouldNotifyStart)
            {
                await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
            }
            else
            {
                await Clients.Caller.SendAsync("WaitingForOpponent", roomCode);
            }
        }

        public Task<bool> CreateRoom(string gameType, string roomCode)
     {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";
            var created = RoomUsers.TryAdd(roomKey, new Dictionary<string, string>());
            return Task.FromResult(created);
        }

        public Task<bool> RoomExists(string gameType, string roomCode)
        {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";
            return Task.FromResult(RoomUsers.ContainsKey(roomKey));
        }

        public async Task ReconnectToRoom(string gameType, string roomCode, string playerId)
        {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";

            if (!RoomUsers.TryGetValue(roomKey, out var users) || !users.ContainsKey(playerId))
            {
                await Clients.Caller.SendAsync("UnauthorizedReconnect", roomCode);
                return;
            }

            lock (users)
            {
                users[playerId] = Context.ConnectionId;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            await Clients.Caller.SendAsync("Reconnected", roomCode);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var kvp in RoomUsers)
            {
                var roomKey = kvp.Key;
                var users = kvp.Value;

                lock (users)
                {
                    var playerToRemove = users.FirstOrDefault(p => p.Value == Context.ConnectionId);
                    if (!string.IsNullOrEmpty(playerToRemove.Key))
                    {
                        users.Remove(playerToRemove.Key);

                        if (users.Count == 0)
                        {
                            RoomUsers.TryRemove(roomKey, out _);
                        }

                        break;
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}