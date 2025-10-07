using System.Collections.Concurrent;
using games;
using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace Services
{
    public class RoomService
    {
        public ConcurrentDictionary<string, Room> Rooms { get; set; } // roomKey -> Room
        public ConcurrentDictionary<string, RoomUser> CodeRoomUsers { get; set; } // playerId -> RoomUser
        public ConcurrentDictionary<string, RoomUser> MatchMakingRoomUsers { get; set; }
        public ConcurrentDictionary<string, string> ActiveMatchmakingSessions { get; set; } // playerId -> roomKey

        public RoomService()
        {
            Rooms = new ConcurrentDictionary<string, Room>();
            CodeRoomUsers = new ConcurrentDictionary<string, RoomUser>();
            MatchMakingRoomUsers = new ConcurrentDictionary<string, RoomUser>();
            ActiveMatchmakingSessions = new ConcurrentDictionary<string, string>();
        }

        public string CreateRoom(string gameType, bool isMatchMaking)
        {
            var roomCode = GenerateRoomCode();
            var roomKey = CreateRoomKey(gameType, roomCode);
            var game = Room.GameTypeToGame(gameType);
            var newRoom = new Room(game, isMatchMaking);
            Rooms[roomKey] = newRoom;
            return roomCode;
        }

        public Room GetRoomByKey(string roomKey)
        {
            return Rooms[roomKey];
        }

        private string GenerateRoomCode(int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rng = new Random();
            string code;

            do
            {
                code = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[rng.Next(s.Length)]).ToArray());
            }
            while (Rooms.ContainsKey(CreateRoomKey("four-in-a-row", code)) ||
                   Rooms.ContainsKey(CreateRoomKey("pair-matching", code)) ||
                   Rooms.ContainsKey(CreateRoomKey("rock-paper-scissors", code)));

            return code;
        }

        public bool RoomExists(string gameType, string roomCode)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            return Rooms.ContainsKey(roomKey);
        }

        public (bool exists, bool isMatchmaking) RoomExistsWithMatchmaking(string gameType, string roomCode)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            if (Rooms.TryGetValue(roomKey, out Room? room))
            {
                return (true, room.IsMatchMaking);
            }
            return (false, false);
        }

        public bool HasActiveMatchmakingSession(string playerId)
        {
            if (ActiveMatchmakingSessions.TryGetValue(playerId, out string? roomKey))
            {
                if (Rooms.TryGetValue(roomKey, out Room? room) && !room.DisconnectedPlayers.ContainsKey(playerId))
                {
                    if (room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                    {
                        return true;
                    }
                    else
                    {
                        ActiveMatchmakingSessions.TryRemove(playerId, out _);
                        return false;
                    }
                }
                else
                {
                    ActiveMatchmakingSessions.TryRemove(playerId, out _);
                }
            }
            return false;
        }

        public string CreateRoomKey(string gameType, string roomCode)
        {
            return $"{gameType}:{roomCode.ToUpper()}";
        }

        public async Task JoinAsPlayerNotMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var room = GetRoomByKey(roomKey);
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;

            if (room.RoomTimerCancellation != null || room.RoomCloseTime != null)
            {
                CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user.Username,
                User = user,
            };

            var existingRoomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers.All(rp => !room.DisconnectedPlayers.ContainsKey(rp.PlayerId));
                if (allConnected)
                {
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                    await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Player reconnected successfully!");
                }
                else
                {
                    await clients.Client(connectionId).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
                }

                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }
                await clients.Client(connectionId).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await clients.Client(connectionId).SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;
                    case PairMatching pairGame:
                        await clients.Client(connectionId).SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;
                    case RockPaperScissors rpsGame:
                        await clients.Client(connectionId).SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
            }

            if (existingRoomUser is null)
            {
                roomPlayers.Add(roomUser);
            }

            bool shouldNotifyStart;
            lock (roomPlayers)
            {
                shouldNotifyStart = roomPlayers.Count == 2;
            }

            if (shouldNotifyStart && !room.GameStarted)
            {
                room.GameStarted = true;
                room.Code = roomKey;
                game.RoomCode = roomKey;
                game.AssignPlayerColors(roomPlayers[0], roomPlayers[1]);

                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }

                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
            }
            else if (room.GameStarted)
            {
                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await clients.Group(roomKey).SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;
                    case PairMatching pairGame:
                        await clients.Group(roomKey).SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;
                    case RockPaperScissors rpsGame:
                        await clients.Group(roomKey).SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
            }
        }

        public RoomUser? GetRoomUser(string roomKey, string playerId, User? user)
        {
            var roomPlayers = GetRoomByKey(roomKey).RoomPlayers;
            return roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);
        }

        public async Task JoinAsPlayerMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var room = GetRoomByKey(roomKey);
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;

            if (room.RoomTimerCancellation != null || room.RoomCloseTime != null)
            {
                CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user.Username,
                User = user,
            };

            var existingRoomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers.All(rp => !room.DisconnectedPlayers.ContainsKey(rp.PlayerId));
                if (allConnected)
                {
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                    await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Player reconnected successfully!");
                }
                else
                {
                    await clients.Client(connectionId).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
                }

                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }
                await clients.Client(connectionId).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await clients.Client(connectionId).SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;
                    case PairMatching pairGame:
                        await clients.Client(connectionId).SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;
                    case RockPaperScissors rpsGame:
                        await clients.Client(connectionId).SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
            }

            if (existingRoomUser is null)
            {
                roomPlayers.Add(roomUser);
            }

            ActiveMatchmakingSessions[playerId] = roomKey;

            bool shouldNotifyStart;
            lock (roomPlayers)
            {
                shouldNotifyStart = roomPlayers.Count == 2;
            }

            if (shouldNotifyStart && !room.GameStarted)
            {
                room.GameStarted = true;
                room.Code = roomKey;
                game.RoomCode = roomKey;
                game.AssignPlayerColors(roomPlayers[0], roomPlayers[1]);
                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;

                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }

                if (room.IsMatchMaking)
                {
                    await clients.Group(roomKey).SendAsync("MatchFound", roomCode);
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                }
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
            }
            else if (room.GameStarted)
            {
                var playerIdToColor = new Dictionary<string, string>();
                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await clients.Group(roomKey).SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;
                    case PairMatching pairGame:
                        await clients.Group(roomKey).SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;
                    case RockPaperScissors rpsGame:
                        await clients.Group(roomKey).SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
            }
        }

        public async Task JoinAsSpectator(string gameType, string roomCode, string playerId, User? user, string connectionId)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user.Username,
                User = user,
            };
            // TODO: Implement spectator logic
        }

        public async Task HandlePlayerDisconnect(string gameType, string roomCode, string playerId, IHubCallerClients clients)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            var roomUser = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
            if (roomUser == null)
                return;

            room.DisconnectedPlayers[playerId] = roomUser;

            // Always restart the timer when a player disconnects
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);

            room.RoomTimerCancellation?.Cancel();
            room.RoomTimerCancellation = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, room.RoomTimerCancellation.Token);
                    await CheckAndCloseRoomIfNeeded(roomKey, clients);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Timer for room {roomKey} was cancelled");
                }
            }, room.RoomTimerCancellation.Token);

            await clients.Group(roomKey).SendAsync("PlayerDisconnected", playerId, "Player disconnected. Room will close in 30 seconds if they don't        reconnect.", room.RoomCloseTime);
        }

        public async Task CheckAndCloseRoomIfNeeded(string roomKey, IHubCallerClients clients)
        {
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            if (room.RoomPlayers.Count == 0)
            {
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing - no players remaining.");
                Rooms.TryRemove(roomKey, out _);
                return;
            }

            if (room.DisconnectedPlayers.Count > 0 && room.RoomCloseTime.HasValue && DateTime.UtcNow >= room.RoomCloseTime.Value)
            {
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing due to disconnected player(s).");
                Rooms.TryRemove(roomKey, out _);

                foreach (var player in room.RoomPlayers)
                {
                    if (room.IsMatchMaking)
                    {
                        MatchMakingRoomUsers.TryRemove(player.PlayerId, out _);
                        ActiveMatchmakingSessions.TryRemove(player.PlayerId, out _);
                    }
                    else
                    {
                        CodeRoomUsers.TryRemove(player.PlayerId, out _);
                    }
                }
            }
        }

        public async Task CleanupExpiredRooms(IHubCallerClients clients)
        {
            var expiredRooms = new List<string>();

            foreach (var kvp in Rooms)
            {
                var room = kvp.Value;
                if (room.DisconnectedPlayers.Count > 0 &&
                    room.RoomCloseTime.HasValue &&
                    DateTime.UtcNow >= room.RoomCloseTime.Value)
                {
                    expiredRooms.Add(kvp.Key);
                }
            }

            foreach (var roomKey in expiredRooms)
            {
                await CheckAndCloseRoomIfNeeded(roomKey, clients);
            }
        }

        public void ClearActiveMatchmakingSession(string playerId)
        {
            ActiveMatchmakingSessions.TryRemove(playerId, out _);
        }

        public async Task CloseRoomAndKickAllPlayers(string roomKey, IHubCallerClients clients, string reason, string? excludePlayerId = null)
        {
            Console.WriteLine($"CloseRoomAndKickAllPlayers called for room {roomKey}, reason: {reason}");

            if (!Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"Room {roomKey} not found in Rooms dictionary");
                return;
            }

            Console.WriteLine($"Found room {roomKey} with {room.RoomPlayers.Count} players");
            Console.WriteLine($"Closing room {roomKey} and kicking all players. Reason: {reason}");

            if (excludePlayerId != null)
            {
                var excludedPlayer = room.RoomPlayers.FirstOrDefault(p => p.PlayerId == excludePlayerId);
                if (excludedPlayer != null)
                {
                    if (room.IsMatchMaking)
                    {
                        MatchMakingRoomUsers.TryRemove(excludePlayerId, out _);
                        ActiveMatchmakingSessions.TryRemove(excludePlayerId, out _);
                    }
                    else
                    {
                        CodeRoomUsers.TryRemove(excludePlayerId, out _);
                    }
                }
            }

            if (excludePlayerId == null)
            {
                await clients.Group(roomKey).SendAsync("RoomClosed", reason, roomKey);
            }
            else
            {
                var otherPlayers = room.RoomPlayers.Where(p => p.PlayerId != excludePlayerId).ToList();
                if (otherPlayers.Count > 0)
                {
                    await clients.Group(roomKey).SendAsync("RoomClosed", reason, roomKey);
                }
            }
            Console.WriteLine($"Sent RoomClosed event to group {roomKey}");

            foreach (var player in room.RoomPlayers)
            {
                if (excludePlayerId != null && player.PlayerId == excludePlayerId)
                    continue;
                Console.WriteLine($"Cleaning up mappings for player {player.PlayerId}");
                if (room.IsMatchMaking)
                {
                    MatchMakingRoomUsers.TryRemove(player.PlayerId, out _);
                    ActiveMatchmakingSessions.TryRemove(player.PlayerId, out _);
                }
                else
                {
                    CodeRoomUsers.TryRemove(player.PlayerId, out _);
                }
            }

            Rooms.TryRemove(roomKey, out _);
            Console.WriteLine($"Room {roomKey} closed and all players kicked");
        }

        private static void CancelRoomTimer(Room room)
        {
            if (room.RoomCloseTime != null || room.RoomTimerCancellation != null)
            {
                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
            }
        }

        public async Task HandlePlayerLeave(string gameType, string roomCode, string playerId, IHubCallerClients clients)
        {
            Console.WriteLine($"RoomService.HandlePlayerLeave called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");
            var roomKey = CreateRoomKey(gameType, roomCode);
            Console.WriteLine($"RoomService: Looking for room with key: {roomKey}");

            if (!Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"RoomService: Room {roomKey} not found");
                return;
            }

            Console.WriteLine($"RoomService: Found room {roomKey}, checking if player {playerId} is in room");
            var roomUser = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
            if (roomUser == null)
            {
                Console.WriteLine($"RoomService: Player {playerId} not found in room {roomKey}");
                return;
            }

            Console.WriteLine($"RoomService: Player {playerId} found in room {roomKey}, proceeding with leave");

            // PATCH: Add player to DisconnectedPlayers before removing from RoomPlayers
            room.DisconnectedPlayers[playerId] = roomUser;

            // Remove player from room
            room.RoomPlayers.RemoveAll(rp => rp.PlayerId == playerId);

            // PATCH: If room is now empty, clear RoomCloseTime and DisconnectedPlayers immediately
            if (room.RoomPlayers.Count == 0)
            {
                room.RoomCloseTime = null;
                room.DisconnectedPlayers.Clear();
                Rooms.TryRemove(roomKey, out _);
                await clients.Group(roomKey).SendAsync("RoomClosed", "Room closed - all players left");
                Console.WriteLine($"Room {roomKey} closed and removed from Rooms dictionary");
                return;
            }

            if (room.RoomPlayers.Count > 0)
            {
                room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
                Console.WriteLine($"RoomService: Set room close time to {room.RoomCloseTime} for room {roomKey}");

                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = new CancellationTokenSource();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"RoomService: Starting 30-second timer for room {roomKey}");
                        await Task.Delay(30000, room.RoomTimerCancellation.Token);

                        if (room.DisconnectedPlayers.Count == 0)
                        {
                            Console.WriteLine($"RoomService: Timer expired but all players are connected in {roomKey}, NOT closing room. Sending PlayerReconnected event and clearing RoomCloseTime.");
                            room.RoomCloseTime = null;
                            room.RoomTimerCancellation?.Cancel();
                            room.RoomTimerCancellation = null;
                            await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "All players reconnected, timer cancelled.");
                            return;
                        }

                        Console.WriteLine($"RoomService: 30-second timer expired for room {roomKey}, closing room");
                        await CloseRoomAndKickAllPlayers(roomKey, clients, "Room closed - player left and timer expired");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Timer for room {roomKey} was cancelled");
                    }
                }, room.RoomTimerCancellation.Token);
            }
            else
            {
                Console.WriteLine($"Room {roomKey} is now empty, closing immediately");
                Rooms.TryRemove(roomKey, out _);
                await clients.Group(roomKey).SendAsync("RoomClosed", "Room closed - all players left");
                Console.WriteLine($"Room {roomKey} closed and removed from Rooms dictionary");
                return;
            }

            await clients.Group(roomKey).SendAsync("PlayerLeft", playerId, "Player left the game", room.RoomCloseTime);
            await clients.Caller.SendAsync("PlayerLeftRoom", "You left the game", room.RoomCloseTime);

            await CheckAndCloseRoomIfNeeded(roomKey, clients);

            if (room.IsMatchMaking)
            {
                MatchMakingRoomUsers.TryRemove(playerId, out _);
            }
            else
            {
                CodeRoomUsers.TryRemove(playerId, out _);
            }

            var removed = ActiveMatchmakingSessions.TryRemove(playerId, out _);
            Console.WriteLine($"RoomService: Removed player {playerId} from ActiveMatchmakingSessions: {removed}");
        }

        public void CleanupInactiveMatchmakingSessions()
        {
            var inactiveSessions = new List<string>();

            foreach (var kvp in ActiveMatchmakingSessions)
            {
                var playerId = kvp.Key;
                var roomKey = kvp.Value;

                if (!Rooms.TryGetValue(roomKey, out Room? room) ||
                    !room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                {
                    inactiveSessions.Add(playerId);
                }
            }

            foreach (var playerId in inactiveSessions)
            {
                ActiveMatchmakingSessions.TryRemove(playerId, out _);
            }
        }

        public async Task ForceRemovePlayerFromAllRooms(string playerId, IHubCallerClients clients)
        {
            var roomsToRemove = Rooms.Where(r =>
                r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) ||
                r.Value.DisconnectedPlayers.ContainsKey(playerId)
            ).ToList();

            foreach (var room in roomsToRemove)
            {
                await CloseRoomAndKickAllPlayers(room.Key, clients, "Player started new matchmaking or left         room.");
            }

            MatchMakingRoomUsers.TryRemove(playerId, out _);
            CodeRoomUsers.TryRemove(playerId, out _);
            ActiveMatchmakingSessions.TryRemove(playerId, out _);
        }
    }
}