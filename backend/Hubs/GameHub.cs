using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using games;
using Models;
using Services;
using System.Security.Principal;

namespace Hubs
{
    public class GameHub : Hub
    {
         private readonly UserService _userService;

        private static readonly ConcurrentDictionary<string, GameInstance> ActiveGames = new();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> RoomCleanupTimers = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> RoomUsers = new();

         public GameHub(UserService userService)
        {
            _userService = userService;
        }

        //Generating room code to make sure it doesnt exist anywhere else
        private static string GenerateRoomCode(int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rng = new Random();
            string code;

            do
            {
                code = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[rng.Next(s.Length)]).ToArray());
            }
            while (RoomUsers.ContainsKey($"four-in-a-row:{code}") ||
                   RoomUsers.ContainsKey($"pair-matching:{code}") ||
                   RoomUsers.ContainsKey($"rock-paper-scissors:{code}"));

            return code;
        }

        public async Task MakeMove(string gameType, string roomCode, string playerId, string command)
        {
            Console.WriteLine("from makemove {0}", gameType);
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
                // need to change this:
                GameInstance game = gameType switch
                {
                    "rock-paper-scissors" => new RockPaperScissors(), // new RockPaperScissors()
                    "four-in-a-row" => new FourInARowGame(),
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
                else if (gameType == "pair-matching")
                {
                    var playerIds = users.Keys.ToList();
                    if (playerIds.Count == 2 && game is PairMatching pairGame)
                    {
                        pairGame.RoomCode = roomKey;
                        pairGame.AssignPlayerColors(playerIds[0], playerIds[1]);
                    }
                    Console.WriteLine($"Assigned colors: {string.Join(", ", ((PairMatching)game).GetPlayerColor(playerIds[0]) + " to " + playerIds[0] + ", " + ((PairMatching)game).GetPlayerColor(playerIds[1]) + " to " + playerIds[1])}");
                    foreach (var pid in playerIds)
                    {
                        var color = ((PairMatching)game).GetPlayerColor(pid);
                        if (users.TryGetValue(pid, out var connId))
                        {
                            await Clients.Client(connId).SendAsync("SetPlayerColor", color);
                        }
                    }
                }
                else if (gameType == "rock-paper-scissors")
                {
                    var playerIds = users.Keys.ToList();
                    if (playerIds.Count == 2 && game is RockPaperScissors rpsGame)
                    {
                        rpsGame.RoomCode = roomKey;
                        rpsGame.AssignPlayerColors(playerIds[0], playerIds[1]);
                    }
                    Console.WriteLine($"Assigned colors: {((RockPaperScissors)game).GetPlayerColor(playerIds[0])} to {playerIds[0]}, {((RockPaperScissors)game).GetPlayerColor(playerIds[1])} to {playerIds[1]}");
                    foreach (var pid in playerIds)
                    {
                        var color = ((RockPaperScissors)game).GetPlayerColor(pid);
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

        public async Task<string?> JoinMatchmaking(string jwtToken, string gameType)
        {
            User user = await _userService.GetUserFromTokenAsync(jwtToken);

            if (user == null)
            {
                await Clients.Caller.SendAsync("UnauthorizedMatchmaking");
                return null;
            }

            Console.WriteLine($"Authenticated matchmaking request from {user.Username}");

            var availableRoom = RoomUsers.FirstOrDefault(kvp =>
                kvp.Key.StartsWith($"{gameType}") &&
                kvp.Value.Count < 2
            );

            string roomCode;

            if (!string.IsNullOrEmpty(availableRoom.Key))
            {
                //Room with user count < 2 exists, joining
                var parts = availableRoom.Key.Split(':');
                roomCode = parts[1];
                await JoinRoom(gameType, roomCode, user.Username);
                await Clients.Caller.SendAsync("MatchFound", roomCode);
            }
            else
            {
                //Rooms are not available, creating new one with random code
                roomCode = GenerateRoomCode();
                await CreateRoom(gameType, roomCode);
                await JoinRoom(gameType, roomCode, user.Username);
                await Clients.Caller.SendAsync("WaitingForOpponent", roomCode);

            }
            return roomCode;
        }

        public async Task JoinAsSpectator(string gameType)
        {
            var activeRoom = ActiveGames.Keys
                .FirstOrDefault(key => key.StartsWith($"{gameType}:"));

            if (activeRoom == null)
            {
                await Clients.Caller.SendAsync("SpectatorJoinFailed", "No active rooms found.");
                return;
            }

            // TODO

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
