using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Hubs
{
    public class GameHub : Hub
    {
        // Thread-safe dictionary to track room participants
        private static readonly ConcurrentDictionary<string, HashSet<string>> RoomUsers = new();

        public async Task JoinRoom(string gameType, string roomCode)
        {
            Console.WriteLine($"Connection {Context.ConnectionId} joining room {roomCode} for game {gameType}");
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";

            // Ensure room exists
            RoomUsers.TryAdd(roomKey, new HashSet<string>());

            var users = RoomUsers[roomKey];

            lock (users)
            {
                if (users.Count >= 2)
                {
                    // Room is full
                    Clients.Caller.SendAsync("RoomFull", roomCode);
                    return;
                }

                users.Add(Context.ConnectionId);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

            if (users.Count == 2)
            {
                // Notify both players to start the game
                await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
            }
            else
            {
                // Notify first player to wait
                await Clients.Caller.SendAsync("WaitingForOpponent", roomCode);
            }
        }

        public Task<bool> RoomExists(string gameType, string roomCode)
        {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";
            return Task.FromResult(RoomUsers.ContainsKey(roomKey));
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string? roomKeyToNotify = null;
            string? remainingConnectionId = null;

            foreach (var kvp in RoomUsers)
            {
                var roomKey = kvp.Key;
                var users = kvp.Value;

                lock (users)
                {
                    if (users.Contains(Context.ConnectionId))
                    {
                        users.Remove(Context.ConnectionId);

                        if (users.Count == 1)
                        {
                            roomKeyToNotify = roomKey;
                            remainingConnectionId = users.First();
                        }

                        if (users.Count == 0)
                        {
                            RoomUsers.TryRemove(roomKey, out _);
                        }

                        break; // Found the room, no need to continue
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            }

            if (roomKeyToNotify != null && remainingConnectionId != null)
            {
                await Clients.Client(remainingConnectionId).SendAsync("PlayerLeft", roomKeyToNotify);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}