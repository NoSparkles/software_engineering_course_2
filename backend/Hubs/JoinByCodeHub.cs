using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using games;
using Models;
using Services;
using System.Security.Principal;

namespace Hubs
{
    public class JoinByCodeHub : Hub, IgameHubInterface
    {
        private static readonly ConcurrentDictionary<string, GameInstance> ActiveGames = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> RoomCleanupTimers = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> RoomUsers = new();

        public async Task HandleCommand(string gameType, string roomCode, string command, string playerId, User? user)
        {
            if (gameType == "four-in-a-row")
            {
                var roomKey = $"{gameType}:{roomCode.ToUpper()}";
                if (ActiveGames.TryGetValue(roomKey, out var game) && game is FourInARowGame fourGame)
                {
                    await fourGame.HandleCommand(playerId, command, Clients);
                }
            }
            else if (gameType == "pair-matching")
            {
                var roomKey = $"{gameType}:{roomCode.ToUpper()}";
                if (ActiveGames.TryGetValue(roomKey, out var game) && game is PairMatching pairGame)
                {
                    await pairGame.HandleCommand(playerId, command, Clients);
                }
            }
            else if (gameType == "rock-paper-scissors")
            {
                var roomKey = $"{gameType}:{roomCode.ToUpper()}";
                if (ActiveGames.TryGetValue(roomKey, out var game) && game is RockPaperScissors rpsGame)
                {
                    await rpsGame.HandleCommand(playerId, command, Clients);
                }
            }
        }

        public async Task Join(string gameType, string roomCode, string playerId, User? user)
        {
            var roomKey = $"{gameType}:{roomCode.ToUpper()}";
            Console.WriteLine($"Player {playerId} joining room {roomCode} for game {gameType}");

            // Ensure the room entry exists
            RoomUsers.TryAdd(roomKey, new Dictionary<string, string>());
            var users = RoomUsers[roomKey];

            bool shouldNotifyStart;
            lock (users)
            {
                users[playerId] = Context.ConnectionId;
                shouldNotifyStart = users.Count == 2;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

            // Get or create game instance
            if (!ActiveGames.TryGetValue(roomKey, out var game))
            {
                game = gameType switch
                {
                    "rock-paper-scissors" => new RockPaperScissors(),
                    "four-in-a-row" => new FourInARowGame(),
                    "pair-matching" => new PairMatching(),
                    _ => throw new Exception("Unknown game type")
                };

                ActiveGames[roomKey] = game;
            }

            if (shouldNotifyStart)
            {
                var playerIds = users.Keys.ToList();

                // Assign colors if needed
                switch (gameType)
                {
                    case "four-in-a-row":
                        if (game is FourInARowGame fourGame)
                        {
                            fourGame.RoomCode = roomKey;
                            fourGame.AssignPlayerColors(playerIds[0], playerIds[1]);

                            foreach (var pid in playerIds)
                                if (users.TryGetValue(pid, out var connId))
                                    await Clients.Client(connId)
                                        .SendAsync("SetPlayerColor", fourGame.GetPlayerColor(pid) ?? "");
                            
                            await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
                        }
                        break;

                    case "pair-matching":
                        if (game is PairMatching pairGame)
                        {
                            pairGame.RoomCode = roomKey;
                            pairGame.AssignPlayerColors(playerIds[0], playerIds[1]);

                            foreach (var pid in playerIds)
                                if (users.TryGetValue(pid, out var connId))
                                    await Clients.Client(connId)
                                        .SendAsync("SetPlayerColor", pairGame.GetPlayerColor(pid) ?? "");
                            
                            await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
                        }
                        break;

                    case "rock-paper-scissors":
                        if (game is RockPaperScissors rpsGame)
                        {
                            rpsGame.RoomCode = roomKey;
                            rpsGame.AssignPlayerColors(playerIds[0], playerIds[1]);

                            foreach (var pid in playerIds)
                                if (users.TryGetValue(pid, out var connId))
                                    await Clients.Client(connId)
                                        .SendAsync("SetPlayerColor", rpsGame.GetPlayerColor(pid) ?? "");
                            
                            await Clients.Group(roomKey).SendAsync("StartGame", roomCode);
                        }
                        break;
                }
            }
            else
            {
                // Player joined AFTER game already started → send current state to this player only
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await Clients.Caller.SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;

                    case PairMatching pairGame:
                        await Clients.Caller.SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;

                    case RockPaperScissors rpsGame:
                        await Clients.Caller.SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
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

                        // if (users.Count == 0)
                        // {
                        //     var cts = new CancellationTokenSource();
                        //     RoomCleanupTimers[roomKey] = cts;

                        //     _ = Task.Delay(TimeSpan.FromMinutes(3), cts.Token).ContinueWith(task =>
                        //     {
                        //         if (!task.IsCanceled)
                        //         {
                        //             RoomUsers.TryRemove(roomKey, out _);
                        //             RoomCleanupTimers.TryRemove(roomKey, out _);
                        //             Console.WriteLine($"Room {roomKey} cleaned up after timeout.");
                        //         }
                        //     });
                        // }

                        break;
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
