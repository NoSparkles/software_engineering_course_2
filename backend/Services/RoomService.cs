using System.Collections.Concurrent;
using Games;
using Extensions;
using Models;
using Models.InMemoryModels;
using System.Threading;

namespace Services
{
    using Microsoft.AspNetCore.SignalR;
    using Hubs;

    public class RoomService : IRoomService
    {
        private readonly IHubContext<SpectatorHub>? SpectatorHubContext;
        public ConcurrentDictionary<string, Room> Rooms { get; set; } // roomKey -> Room
        public ConcurrentDictionary<string, RoomUser> CodeRoomUsers { get; set; } // playerId -> RoomUser
        public ConcurrentDictionary<string, RoomUser> MatchMakingRoomUsers { get; set; }
        public ConcurrentDictionary<string, string> ActiveMatchmakingSessions { get; set; } // playerId -> roomKey

        public RoomService(IHubContext<SpectatorHub> spectatorHubContext)
        {
            SpectatorHubContext = spectatorHubContext;
            Rooms = new ConcurrentDictionary<string, Room>();
            CodeRoomUsers = new ConcurrentDictionary<string, RoomUser>();
            MatchMakingRoomUsers = new ConcurrentDictionary<string, RoomUser>();
            ActiveMatchmakingSessions = new ConcurrentDictionary<string, string>();
        }

        public string CreateRoom(string gameType, bool isMatchMaking)
        {
            var roomCode = GenerateRoomCode();
            var roomKey = gameType.ToRoomKey(roomCode);
            var game = Room.GameTypeToGame(gameType);
            var newRoom = new Room(game, isMatchMaking);
            Rooms[roomKey] = newRoom;
            return roomCode;
        }

        public Room? GetRoomByKey(string roomKey)
        {
            Rooms.TryGetValue(roomKey, out Room? room);
            return room;
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
            while (Rooms.ContainsKey("four-in-a-row".ToRoomKey(code)) ||
                   Rooms.ContainsKey("pair-matching".ToRoomKey(code)) ||
                   Rooms.ContainsKey("rock-paper-scissors".ToRoomKey(code)));

            return code;
        }

        public bool RoomExists(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            return Rooms.ContainsKey(roomKey);
        }

        public (bool exists, bool isMatchmaking) RoomExistsWithMatchmaking(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
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

        public async Task ReportWin(string playerId, IHubCallerClients clients)
        {
            foreach (var room in Rooms.Values)
            {
                if (room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                {
                    await room.Game.ReportWin(playerId, clients); // pass clients
                    await clients.Group(room.Code).SendAsync("PlayerWon", playerId);
                    break;
                }
            }
        }

        public async Task JoinAsPlayerNotMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            var room = GetRoomByKey(roomKey);
            
            if (room == null)
            {
                Console.WriteLine($"JoinAsPlayerNotMatchMaking failed: Room {roomKey} does not exist");
                await clients.Client(connectionId).SendAsync("JoinFailed", "Room no longer exists. It may have been closed.");
                return;
            }
            
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;

            if (room.RoomTimerCancellation != null || room.RoomCloseTime != null)
            {
                await CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user?.Username,
                User = user,
            };

            var existingRoomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers?.All(rp => rp.PlayerId != null && !room.DisconnectedPlayers.ContainsKey(rp.PlayerId)) ?? false;
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
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                        {
                            var color = game.GetPlayerColor(rp);
                            if (color != null)
                                playerIdToColor[rp.PlayerId] = color;
                        }
                    }
                }
                await clients.Client(connectionId).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        var moveState = fourGame.GetGameState();
                        await clients.Client(connectionId).SendAsync("ReceiveMove", moveState);
                        // also notify spectators
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", moveState);
                        break;
                    case PairMatching pairGame:
                        var boardState = pairGame.GetGameState();
                        await clients.Client(connectionId).SendAsync("ReceiveBoard", boardState);
                        if (pairGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", pairGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", boardState);
                        break;
                    case RockPaperScissors rpsGame:
                        var rpsState = rpsGame.GetGameStatePublic();
                        await clients.Client(connectionId).SendAsync("ReceiveRpsState", rpsState);
                        if (rpsGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", rpsGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", rpsState);
                        break;
                }
            }

            if (existingRoomUser is null)
            {
                roomPlayers.Add(roomUser);
                // Send RoomPlayersUpdate when a new player joins
                var allPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null).Select(p => p.PlayerId!).ToList() ?? new List<string>();
                await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", allPlayers);
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
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                            playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                    }
                }

                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
                
                // Send RoomPlayersUpdate to all players so they can see updated room state
                var allPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null).Select(p => p.PlayerId!).ToList() ?? [];
                await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", allPlayers);
                
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
            }
            else if (room.GameStarted)
            {
                var playerIdToColor = new Dictionary<string, string>();
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                            playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                    }
                }
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        var moveState2 = fourGame.GetGameState();
                        await clients.Group(roomKey).SendAsync("ReceiveMove", moveState2);
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", moveState2);
                        break;
                    case PairMatching pairGame:
                        var boardState2 = pairGame.GetGameState();
                        await clients.Group(roomKey).SendAsync("ReceiveBoard", boardState2);
                        if (pairGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", pairGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", boardState2);
                        break;
                    case RockPaperScissors rpsGame:
                        var rpsState2 = rpsGame.GetGameStatePublic();
                        await clients.Group(roomKey).SendAsync("ReceiveRpsState", rpsState2);
                        if (rpsGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", rpsGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", rpsState2);
                        break;
                }
            }
        }

        public RoomUser? GetRoomUser(string roomKey, string playerId, User? user)
        {
            var room = GetRoomByKey(roomKey);
            if (room == null) return null;
            
            var roomPlayers = room.RoomPlayers;
            return roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);
        }

        public async Task JoinAsPlayerMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            var room = GetRoomByKey(roomKey);
            
            if (room == null)
            {
                Console.WriteLine($"JoinAsPlayerMatchMaking failed: Room {roomKey} does not exist");
                await clients.Client(connectionId).SendAsync("JoinFailed", "Room no longer exists. It may have been closed.");
                return;
            }
            
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;

            if (room.RoomTimerCancellation != null || room.RoomCloseTime != null)
            {
                await CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user?.Username,
                User = user,
            };

            var existingRoomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers?.All(rp => rp.PlayerId != null && !room.DisconnectedPlayers.ContainsKey(rp.PlayerId)) ?? false;
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
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                        {
                            var color = game.GetPlayerColor(rp);
                            if (color != null)
                                playerIdToColor[rp.PlayerId] = color;
                        }
                    }
                }
                await clients.Client(connectionId).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        var moveState3 = fourGame.GetGameState();
                        await clients.Client(connectionId).SendAsync("ReceiveMove", moveState3);
                        if( fourGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", fourGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", moveState3);
                        break;
                    case PairMatching pairGame:
                        var boardState3 = pairGame.GetGameState();
                        await clients.Client(connectionId).SendAsync("ReceiveBoard", boardState3);
                        if (pairGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", pairGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", boardState3);
                        break;
                    case RockPaperScissors rpsGame:
                        var rpsState3 = rpsGame.GetGameStatePublic();
                        await clients.Client(connectionId).SendAsync("ReceiveRpsState", rpsState3);
                        if (rpsGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", rpsGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", rpsState3);
                        break;
                }
            }

            if (existingRoomUser is null)
            {
                roomPlayers.Add(roomUser);
                // Send RoomPlayersUpdate when a new player joins
                var allPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null).Select(p => p.PlayerId!).ToList() ?? new List<string>();
                await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", allPlayers);
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
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                        {
                            var color = game.GetPlayerColor(rp);
                            if (color != null)
                                playerIdToColor[rp.PlayerId] = color;
                        }
                    }
                }

                if (room.IsMatchMaking)
                {
                    await clients.Group(roomKey).SendAsync("MatchFound", roomCode);
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                }
                
                // Send RoomPlayersUpdate to all players so they can see updated room state
                var allPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null).Select(p => p.PlayerId!).ToList() ?? [];
                await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", allPlayers);
                
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
            }
            else if (room.GameStarted)
            {
                var playerIdToColor = new Dictionary<string, string>();
                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
                if (room.RoomPlayers != null)
                {
                    foreach (var rp in room.RoomPlayers)
                    {
                        if (rp.PlayerId != null)
                        {
                            var color = game.GetPlayerColor(rp);
                            if (color != null)
                                playerIdToColor[rp.PlayerId] = color;
                        }
                    }
                }
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                if (SpectatorHubContext != null)
                    await SpectatorHubContext.Clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                switch (game)
                {
                    case FourInARowGame fourGame:
                        var moveState4 = fourGame.GetGameState();
                        await clients.Group(roomKey).SendAsync("ReceiveMove", moveState4);
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", moveState4);
                        break;
                    case PairMatching pairGame:
                        var boardState4 = pairGame.GetGameState();
                        await clients.Group(roomKey).SendAsync("ReceiveBoard", boardState4);
                        if (pairGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", pairGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", boardState4);
                        break;
                    case RockPaperScissors rpsGame:
                        var rpsState4 = rpsGame.GetGameStatePublic();
                        await clients.Group(roomKey).SendAsync("ReceiveRpsState", rpsState4);
                        if (rpsGame.WinnerColor != "")
                        {
                            await clients.Client(connectionId).SendAsync("GameOver", rpsGame.WinnerColor);
                        }
                        if (SpectatorHubContext != null)
                            await SpectatorHubContext.Clients.Group(roomKey).SendAsync("GameStateUpdate", rpsState4);
                        break;
                }
            }
        }

        public async Task JoinAsSpectator(string gameType, string roomCode, string playerId, User? user = null)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            var roomUser = new RoomUser
            {
                PlayerId = playerId,
                Username = user?.Username,
                User = user,
            };
            // Add spectator to room's spectator list if room exists
            if (!Rooms.TryGetValue(roomKey, out var room))
            {
                Console.WriteLine($"JoinAsSpectator failed: Room {roomKey} does not exist");
                return;
            }

            // Avoid duplicate spectator entries
            lock (room.RoomSpectators)
            {
                var exists = room.RoomSpectators.Any(s => s.PlayerId == playerId);
                if (!exists)
                {
                    room.RoomSpectators.Add(roomUser);
                }
            }


            Console.WriteLine($"Spectator {playerId} joined room {roomKey}");

            // No SignalR Clients reference here - hub will manage group/join notifications
            await Task.CompletedTask;
        }

        public async Task HandlePlayerDisconnect(string gameType, string roomCode, string playerId, IHubCallerClients clients)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            var roomUser = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
            if (roomUser == null)
                return;

            Console.WriteLine($"RoomService: Player {playerId} disconnected from room {roomKey}");

            room.DisconnectedPlayers[playerId] = roomUser;

            // Get remaining players BEFORE checking if all are disconnected
            var remainingPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null && p.PlayerId != playerId).Select(p => p.PlayerId!).ToList() ?? new List<string>();

            // Check if all players are now disconnected
            bool allPlayersDisconnected = room.RoomPlayers?.All(rp => rp.PlayerId != null && room.DisconnectedPlayers.ContainsKey(rp.PlayerId)) ?? false;
            
            if (allPlayersDisconnected)
            {
                // All players disconnected, close room immediately and clean up
                Console.WriteLine($"Room {roomKey} - all players disconnected, closing room and cleaning up");
                await clients.Group(roomKey).SendAsync("RoomClosed", "All players disconnected. Room closed.");
                
                // Clean up player mappings
                if (room.RoomPlayers != null)
                {
                    foreach (var player in room.RoomPlayers)
                    {
                        if (player.PlayerId == null) continue;
                        
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
                
                // Remove and dispose the room
                if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                {
                    removedRoom?.Dispose();
                }
                Console.WriteLine($"Room {roomKey} closed and disposed - all players disconnected");
                return;
            }

            // Start timer for remaining players
            await StartRoomTimer(roomKey, room, clients, "Player disconnected");

            // Send events to remaining players (excluding the disconnected player)
            Console.WriteLine($"RoomService: Sending RoomPlayersUpdate to group {roomKey} with players: {string.Join(", ", remainingPlayers)}");
            await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", remainingPlayers);
            
            // Convert DateTime to ISO 8601 string for JavaScript compatibility
            string? roomCloseTimeString = room.RoomCloseTime?.ToString("o"); // "o" format = ISO 8601
            Console.WriteLine($"RoomService: Sending PlayerDisconnected to group {roomKey} with roomCloseTime: {roomCloseTimeString}");
            await clients.Group(roomKey).SendAsync("PlayerDisconnected", playerId, "Player disconnected. Room will close in 30 seconds if they don't reconnect.", roomCloseTimeString);
        }

        public async Task CheckAndCloseRoomIfNeeded(string roomKey, IHubCallerClients clients)
        {
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            if (room.RoomPlayers.Count == 0)
            {
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing - no players remaining.");
                if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                {
                    removedRoom?.Dispose();
                }
                return;
            }

            if (room.DisconnectedPlayers.Count > 0 && room.RoomCloseTime.HasValue && DateTime.UtcNow >= room.RoomCloseTime.Value)
            {
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing due to disconnected player(s).");
                if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                {
                    removedRoom?.Dispose();
                }

                foreach (var player in room.RoomPlayers)
                {
                    if (player.PlayerId == null) continue;
                    
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
            Console.WriteLine($"CloseRoomAndKickAllPlayers called for room {roomKey}, reason: {reason}, excludePlayerId: {excludePlayerId}");

            if (!Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"Room {roomKey} not found in Rooms dictionary");
                return;
            }

            Console.WriteLine($"Found room {roomKey} with {room.RoomPlayers.Count} players");
            Console.WriteLine($"Closing room {roomKey} and kicking all players. Reason: {reason}");

            // Clean up excluded player first if provided
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

            // For matchmaking rooms, check if remaining players have already joined other rooms
            // before sending RoomClosed event
            bool shouldSendRoomClosed = true;
            if (room.IsMatchMaking && excludePlayerId != null)
            {
                var otherPlayers = room.RoomPlayers.Where(p => p.PlayerId != excludePlayerId).ToList();
                if (otherPlayers.Count > 0)
                {
                    // Check if any remaining player is still actively in THIS room
                    bool anyPlayerStillInThisRoom = false;
                    foreach (var player in otherPlayers)
                    {
                        if (player.PlayerId != null && ActiveMatchmakingSessions.TryGetValue(player.PlayerId, out string? activeRoomKey))
                        {
                            if (activeRoomKey == roomKey)
                            {
                                anyPlayerStillInThisRoom = true;
                                Console.WriteLine($"Player {player.PlayerId} is still in room {roomKey}");
                                break;
                            }
                            else
                            {
                                Console.WriteLine($"Player {player.PlayerId} already in different room {activeRoomKey}");
                            }
                        }
                    }
                    
                    if (!anyPlayerStillInThisRoom)
                    {
                        Console.WriteLine($"No players are still in room {roomKey}, skipping RoomClosed event");
                        shouldSendRoomClosed = false;
                    }
                }
            }

            if (shouldSendRoomClosed)
            {
                Console.WriteLine($"Sending RoomClosed event to group {roomKey}");
                await clients.Group(roomKey).SendAsync("RoomClosed", reason, roomKey);
            }

            foreach (var player in room.RoomPlayers)
            {
                if (player.PlayerId == null) continue;
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

            if (Rooms.TryRemove(roomKey, out Room? removedRoom))
            {
                removedRoom?.Dispose();
            }
            Console.WriteLine($"Room {roomKey} closed and all players kicked");
        }

        private static async Task CancelRoomTimer(Room room)
        {
            if (room.RoomCloseTime != null || room.RoomTimerCancellation != null)
            {
                CancellationTokenSource? cts = null;
                lock (room)
                {
                    room.RoomCloseTime = null;
                    cts = room.RoomTimerCancellation;
                    room.RoomTimerCancellation = null;
                }
                try
                {
                    cts?.Cancel();
                    cts?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // already disposed - ignore
                }
            }
            await Task.CompletedTask; // Suppress async warning
        }

        public Task StartRoomTimer(string roomKey, Room room, IHubCallerClients clients, string reason)
        {
            // Cancel any existing timer safely
            CancellationTokenSource? existing = null;
            lock (room)
            {
                existing = room.RoomTimerCancellation;
                room.RoomTimerCancellation = null;
            }
            try { 
                existing?.Cancel(); 
                existing?.Dispose(); 
            } catch (ObjectDisposedException) { }

            // Set new timer
            var newCts = new CancellationTokenSource();
            lock (room)
            {
                room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
                room.RoomTimerCancellation = newCts;
            }
            
            Console.WriteLine($"RoomService: Starting 30-second timer for room {roomKey} - {reason}");
            Console.WriteLine($"RoomService: Room {roomKey} has {room.RoomPlayers.Count} players and {room.DisconnectedPlayers.Count} disconnected players");
            Console.WriteLine($"RoomService: Timer will expire at {room.RoomCloseTime:yyyy-MM-dd HH:mm:ss} UTC");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, newCts.Token);

                    // Re-check room state when timer expires
                    if (!Rooms.TryGetValue(roomKey, out Room? currentRoom) || currentRoom.RoomCloseTime == null)
                    {
                        Console.WriteLine($"RoomService: Timer expired but room {roomKey} no longer exists or timer was cleared");
                        return;
                    }

                    // Check if all players have reconnected (no disconnected players)
                    if (currentRoom.DisconnectedPlayers.Count == 0)
                    {
                        Console.WriteLine($"RoomService: Timer expired but all players are connected in {roomKey}, NOT closing room");
                        lock (currentRoom)
                        {
                            currentRoom.RoomCloseTime = null;
                            var ctsLocal = currentRoom.RoomTimerCancellation;
                            currentRoom.RoomTimerCancellation = null;
                            try { ctsLocal?.Cancel(); } catch (ObjectDisposedException) { }
                        }
                        await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "All players reconnected, timer cancelled.");
                        return;
                    }

                    // Check if we have enough players to keep the room open
                    if (currentRoom.RoomPlayers.Count >= 2)
                    {
                        Console.WriteLine($"RoomService: Timer expired but room {roomKey} has enough players ({currentRoom.RoomPlayers.Count}), NOT closing room");
                        lock (currentRoom)
                        {
                            currentRoom.RoomCloseTime = null;
                            var ctsLocal2 = currentRoom.RoomTimerCancellation;
                            currentRoom.RoomTimerCancellation = null;
                            try { ctsLocal2?.Cancel(); } catch (ObjectDisposedException) { }
                        }
                        await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Room has enough players, timer cancelled.");
                        return;
                    }

                    Console.WriteLine($"RoomService: 30-second timer expired for room {roomKey}, closing room");
                    await CloseRoomAndKickAllPlayers(roomKey, clients, $"Room closed - {reason} and timer expired");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Timer for room {roomKey} was cancelled");
                }
            }, newCts.Token);
            
            return Task.CompletedTask;
        }

        public async Task HandlePlayerLeave(string gameType, string roomCode, string playerId, IHubCallerClients clients)
        {
            Console.WriteLine($"RoomService.HandlePlayerLeave called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");
            var roomKey = gameType.ToRoomKey(roomCode);
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

            // Add player to DisconnectedPlayers before removing from RoomPlayers
            room.DisconnectedPlayers[playerId] = roomUser;

            // Get remaining players BEFORE removing the leaving player
            var remainingPlayers = room.RoomPlayers?.Where(p => p.PlayerId != null && p.PlayerId != playerId).Select(p => p.PlayerId!).ToList() ?? [];

            // Remove player from room
            room.RoomPlayers?.RemoveAll(rp => rp.PlayerId == playerId);

            // If room is now empty, close it immediately
            if (room.RoomPlayers?.Count == 0)
            {
                room.RoomCloseTime = null;
                room.DisconnectedPlayers.Clear();
                if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                {
                    removedRoom?.Dispose();
                }
                await clients.Group(roomKey).SendAsync("RoomClosed", "Room closed - all players left", roomKey);
                Console.WriteLine($"Room {roomKey} closed and removed from Rooms dictionary");
                return;
            }
            if (room.IsMatchMaking)
            {
                Console.WriteLine($"Matchmaking room {roomKey} - closing immediately as player left");

                // Clean up all remaining players
                if (room.RoomPlayers != null)
                {
                    foreach (var player in room.RoomPlayers)
                    {
                        if (player.PlayerId == null) continue;
                        MatchMakingRoomUsers.TryRemove(player.PlayerId, out _);
                        ActiveMatchmakingSessions.TryRemove(player.PlayerId, out _);
                    }
                }

                // Notify remaining players
                await clients.Group(roomKey).SendAsync("RoomClosed", "Matchmaking room closed - opponent left", roomKey);

                // Clean up the leaving player's mappings
                MatchMakingRoomUsers.TryRemove(playerId, out _);
                ActiveMatchmakingSessions.TryRemove(playerId, out _);

                // Remove and dispose the room
                if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                {
                    removedRoom?.Dispose();
                }

                Console.WriteLine($"Matchmaking room {roomKey} closed immediately");
                return;
            }

            // For non-matchmaking rooms (code-based), start timer for reconnection
            await StartRoomTimer(roomKey, room, clients, "Player left the room");

            // Send events to remaining players (excluding the leaving player)
            Console.WriteLine($"RoomService: Sending RoomPlayersUpdate to group {roomKey} with players: {string.Join(", ", remainingPlayers)}");
            await clients.Group(roomKey).SendAsync("RoomPlayersUpdate", remainingPlayers);
            Console.WriteLine($"RoomService: Sending PlayerLeft to group {roomKey} with roomCloseTime: {room.RoomCloseTime}");
            await clients.Group(roomKey).SendAsync("PlayerLeft", playerId, "Player left the game", room.RoomCloseTime);

            // Send event to the leaving player
            await clients.Caller.SendAsync("PlayerLeftRoom", "You left the game", room.RoomCloseTime);

            // Clean up player mappings for code-based rooms
            CodeRoomUsers.TryRemove(playerId, out _);

            Console.WriteLine($"RoomService: Removed player {playerId} from mappings");
        }

        public void CleanupInactiveMatchmakingSessions()
        {
            var inactiveSessions = new List<string>();

            foreach (var kvp in ActiveMatchmakingSessions) //Iterating through collection
            {
                var playerId = kvp.Key;
                var roomKey = kvp.Value;

                if (!Rooms.TryGetValue(roomKey, out Room? room) ||
                    !room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                {
                    inactiveSessions.Add(playerId);
                }
            }

            foreach (var playerId in inactiveSessions) //Iterating through collection
            {
                ActiveMatchmakingSessions.TryRemove(playerId, out _);
            }
        }

        public (string? gameType, string? roomCode, bool isMatchmaking) GetUserCurrentGame(string username)
        {
            // Check all rooms to see if this user is currently playing
            foreach (var roomKvp in Rooms)
            {
                var room = roomKvp.Value;
                var roomKey = roomKvp.Key;
                
                // Check if user is in the room's players list
                var userInRoom = room.RoomPlayers.Any(rp => rp.PlayerId == username || rp.Username == username);
                
                if (userInRoom)
                {
                    // Extract game type and room code from room key
                    var parts = roomKey.Split(':');
                    if (parts.Length == 2)
                    {
                        var gameType = parts[0];
                        var roomCode = parts[1];
                        return (gameType, roomCode, room.IsMatchMaking);
                    }
                }
            }
            
            return (null, null, false);
        }

        public async Task ForceRemovePlayerFromAllRooms(string playerId, IHubCallerClients clients)
        {
            var roomsWithPlayer = Rooms.Where(r =>
                r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) ||
                r.Value.DisconnectedPlayers.ContainsKey(playerId)
            ).ToList();
        
            if (roomsWithPlayer.Count == 0)
            {
                MatchMakingRoomUsers.TryRemove(playerId, out _);
                CodeRoomUsers.TryRemove(playerId, out _);
                ActiveMatchmakingSessions.TryRemove(playerId, out _);
                return;
            }
        
            foreach (var roomKvp in roomsWithPlayer)
            {
                var roomKey = roomKvp.Key;
                var room = roomKvp.Value;
                
                // For matchmaking rooms, close immediately and kick all players
                if (room.IsMatchMaking)
                {
                    Console.WriteLine($"ForceRemovePlayerFromAllRooms: Closing matchmaking room {roomKey} as player {playerId} is starting a new search");
                    // Pass playerId as excluded player so they don't receive RoomClosed event
                    await CloseRoomAndKickAllPlayers(roomKey, clients, "A player started a new matchmaking search", excludePlayerId: playerId);
                    continue;
                }
                
                // For non-matchmaking rooms, remove player but keep room open for others
                // Remove from disconnected players if present
                room.DisconnectedPlayers.Remove(playerId);
                
                // Remove from room players
                var playerInRoom = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
                if (playerInRoom != null)
                {
                    room.RoomPlayers.Remove(playerInRoom);
                }
                
                // If room is now empty, close it
                if (room.RoomPlayers.Count == 0)
                {
                    if (Rooms.TryRemove(roomKey, out Room? removedRoom))
                    {
                        removedRoom?.Dispose();
                    }
                    await clients.Group(roomKey).SendAsync("RoomClosed", "Room closed - all players left.", roomKey);
                }
                else
                {
                    // Room still has OTHER players, notify them and close the room
                    // Pass playerId as excluded player so they don't receive RoomClosed event
                    await CloseRoomAndKickAllPlayers(roomKey, clients, "A player left to start a new session", excludePlayerId: playerId);
                }
            }
        
            // Clean up mappings
            MatchMakingRoomUsers.TryRemove(playerId, out _);
            CodeRoomUsers.TryRemove(playerId, out _);
            ActiveMatchmakingSessions.TryRemove(playerId, out _);
        }
    }
}