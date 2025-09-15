using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using games;

namespace Hubs
{
    public class GameHub : Hub
    {
        private static readonly ConcurrentDictionary<string, GameInstance> ActiveGames = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> RoomCleanupTimers = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> RoomUsers = new();
        
        public async Task MakeMove(string gameType, string roomCode, string playerId, string command)
        {
            if (gameType == "four-in-a-row")
            {
                var roomKey = $"{gameType}:{roomCode.ToUpper()}"; 
                if (ActiveGames.TryGetValue(roomKey, out var game) && game is FourInARowGame fourGame)
                {
                    await fourGame.HandleCommand(playerId, command, Clients);
                }
            }
        }
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
                GameInstance game = gameType switch
                {
                    // to be implemented
                    "rock-paper-scissors" => null, //new RockPaperScissorsGame(),
                    "four-in-a-row" => new FourInARowGame(), //new FourInARowGame(),
                    "pair-matching" => new PairMatching(),
                    _ => throw new Exception("Unknown game type")
                };

                if (gameType == "four-in-a-row")
                {
                    var playerIds = users.Keys.ToList();
                    if (playerIds.Count == 2 && game is FourInARowGame fourGame)
                    {
                        fourGame.RoomCode = roomKey;
                        fourGame.AssignPlayerColors(playerIds[0], playerIds[1]);
                    }
                    Console.WriteLine($"Assigned colors: {string.Join(", ", ((FourInARowGame)game).GetPlayerColor(playerIds[0]) + " to " + playerIds[0] + ", " + ((FourInARowGame)game).GetPlayerColor(playerIds[1]) + " to " + playerIds[1])}");
                    foreach (var pid in playerIds)
                    {
                        var color = ((FourInARowGame)game).GetPlayerColor(pid);
                        if (users.TryGetValue(pid, out var connId))
                        {
                            await Clients.Client(connId).SendAsync("SetPlayerColor", color);
                        }
                    }


                }

                ActiveGames[roomKey] = game;

                await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
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
            Console.WriteLine($"Checking if room {roomCode} exists for game {gameType}");
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
                            var cts = new CancellationTokenSource();
                            RoomCleanupTimers[roomKey] = cts;

                            _ = Task.Delay(TimeSpan.FromMinutes(3), cts.Token).ContinueWith(task =>
                            {
                                if (!task.IsCanceled)
                                {
                                    RoomUsers.TryRemove(roomKey, out _);
                                    RoomCleanupTimers.TryRemove(roomKey, out _);
                                    Console.WriteLine($"Room {roomKey} cleaned up after timeout.");
                                }
                            });
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