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
        public ConcurrentDictionary<string, RoomUser> MatchMakingRoomUsers { get; set; } // 
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
                // Check if the room still exists and is active
                if (Rooms.TryGetValue(roomKey, out Room? room) && !room.DisconnectedPlayers.ContainsKey(playerId))
                {
                    // Double-check that the player is actually in the room
                    if (room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                    {
                        return true;
                    }
                    else
                    {
                        // Player is not in the room, clean up
                        ActiveMatchmakingSessions.TryRemove(playerId, out _);
                        return false;
                    }
                }
                else
                {
                    // Clean up inactive session
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
                Console.WriteLine($"[RoomService] Player joined {roomKey}. Cancelling room timer.");
                CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            // Check if this is a reconnection
            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers.All(rp => !room.DisconnectedPlayers.ContainsKey(rp.PlayerId));
                if (allConnected)
                {
                    if (room.RoomCloseTime != null || room.RoomTimerCancellation != null)
                    {
                        Console.WriteLine($"[RoomService] All players reconnected in {roomKey}. Cancelling timer and clearing RoomCloseTime.");
                    }
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                    await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Player reconnected successfully!");
                }
                else
                {
                    await clients.Client(connectionId).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
                }

                // Send current game state and color to reconnecting player
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

            if (roomUser is null)
            {
                roomPlayers.Add(new RoomUser(playerId, true, user));
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

                // Send StartGame and SetPlayerColor to all players in the room
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
                Console.WriteLine($"[RoomService] Player joined {roomKey}. Cancelling room timer.");
                CancelRoomTimer(room);
                await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Timer cancelled: a player joined.");
            }

            var roomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            // Check if this is a reconnection
            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);

                bool allConnected = room.RoomPlayers.All(rp => !room.DisconnectedPlayers.ContainsKey(rp.PlayerId));
                if (allConnected)
                {
                    if (room.RoomCloseTime != null || room.RoomTimerCancellation != null)
                    {
                        Console.WriteLine($"[RoomService] All players reconnected in {roomKey}. Cancelling timer and clearing RoomCloseTime.");
                    }
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                    // --- FIX: Instantly notify all clients to clear timer/banner as soon as both are connected ---
                    await clients.Group(roomKey).SendAsync("PlayerReconnected", null, "Player reconnected successfully!");
                }
                else
                {
                    await clients.Client(connectionId).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
                }

                // Send current game state and color to reconnecting player
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

            if (roomUser is null)
            {
                roomPlayers.Add(new RoomUser(playerId, true, user));
                Console.WriteLine($"Added player {playerId} to room {roomCode}. Total players: {roomPlayers.Count}");
            }
            else
            {
                Console.WriteLine($"Player {playerId} already exists in room {roomCode}. Total players: {roomPlayers.Count}");
            }

            // Track active matchmaking session
            ActiveMatchmakingSessions[playerId] = roomKey;

            bool shouldNotifyStart;
            lock (roomPlayers)
            {
                shouldNotifyStart = roomPlayers.Count == 2;
            }
            Console.WriteLine($"Room {roomCode}: {roomPlayers.Count} players, GameStarted: {room.GameStarted}, shouldNotifyStart: {shouldNotifyStart}");

            if (shouldNotifyStart && !room.GameStarted)
            {
                Console.WriteLine($"Starting game for room {roomCode} with {roomPlayers.Count} players");
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
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                }

                // Send MatchFound, StartGame and SetPlayerColor to all players in the room
                if (room.IsMatchMaking)
                {
                    await clients.Group(roomKey).SendAsync("MatchFound", roomCode);
                    room.RoomCloseTime = null;
                    room.RoomTimerCancellation?.Cancel();
                    room.RoomTimerCancellation = null;
                }
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                Console.WriteLine($"Sent MatchFound, StartGame and SetPlayerColor to group {roomKey}");
                room.RoomCloseTime = null;
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
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
            var RoomUser = new RoomUser(playerId, false, user);
            // TODO
        }

        public async Task HandlePlayerDisconnect(string gameType, string roomCode, string playerId, IHubCallerClients clients)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            // Check if player is in the room
            var roomUser = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
            if (roomUser == null)
                return;

            // Mark player as disconnected
            room.DisconnectedPlayers[playerId] = DateTime.UtcNow;

            // Set room close time if not already set
            if (room.RoomCloseTime == null)
            {
                room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);

                // Cancel any existing timer
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = new CancellationTokenSource();

                // Start a timer to close the room if player doesn't reconnect
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
            }

            // Notify all players in the room about the disconnection
            await clients.Group(roomKey).SendAsync("PlayerDisconnected", playerId, "Player disconnected. Room will close in 30 seconds if they don't        reconnect.", room.RoomCloseTime);
        }


        public async Task CheckAndCloseRoomIfNeeded(string roomKey, IHubCallerClients clients)
        {
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            // Check if room has 0 players - close immediately
            if (room.RoomPlayers.Count == 0)
            {
                // Notify all remaining players that the room is closing
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing - no players remaining.");

                // Remove the room
                Rooms.TryRemove(roomKey, out _);
                return;
            }

            // Only close the room if there are still disconnected players
            if (room.DisconnectedPlayers.Count > 0 && room.RoomCloseTime.HasValue && DateTime.UtcNow >= room.RoomCloseTime.Value)
            {
                // Notify all remaining players that the room is closing
                await clients.Group(roomKey).SendAsync("RoomClosing", "Room is closing due to disconnected player(s).");

                // Remove the room
                Rooms.TryRemove(roomKey, out _);

                // Clean up user mappings
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

            // Remove excluded player from mappings BEFORE sending RoomClosed
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

            // Only send RoomClosed to the group if there are players other than the excluded player
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

            // Clean up user mappings for all other players
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

            // Remove the room
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
            // Check if player is in the room
            var roomUser = room.RoomPlayers.Find(rp => rp.PlayerId == playerId);
            if (roomUser == null)
            {
                Console.WriteLine($"RoomService: Player {playerId} not found in room {roomKey}");
                return;
            }

            Console.WriteLine($"RoomService: Player {playerId} found in room {roomKey}, proceeding with leave");


            // Remove player from room
            room.RoomPlayers.RemoveAll(rp => rp.PlayerId == playerId);

            // Remove from disconnected players if present
            room.DisconnectedPlayers.Remove(playerId);

            // Set room close time if there are still players remaining
            if (room.RoomPlayers.Count > 0)
            {
                room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
                Console.WriteLine($"RoomService: Set room close time to {room.RoomCloseTime} for room {roomKey}");

                // Cancel any existing timer
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = new CancellationTokenSource();

                // Start a timer to close the room and kick all remaining players
                _ = Task.Run(async () =>
                {
                    try
                    {

                        Console.WriteLine($"RoomService: Starting 30-second timer for room {roomKey}");
                        await Task.Delay(30000, room.RoomTimerCancellation.Token); // Wait 30 seconds

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
                // If room is now empty, close it immediately
                Console.WriteLine($"Room {roomKey} is now empty, closing immediately");
                Rooms.TryRemove(roomKey, out _);
                await clients.Group(roomKey).SendAsync("RoomClosed", "Room closed - all players left");
                Console.WriteLine($"Room {roomKey} closed and removed from Rooms dictionary");
                return;
            }

            // Notify other players that this player left
            await clients.Group(roomKey).SendAsync("PlayerLeft", playerId, "Player left the game", room.RoomCloseTime);

            // Also notify the leaving player about the room close time so they can see the timer in Return to Game banner
            await clients.Caller.SendAsync("PlayerLeftRoom", "You left the game", room.RoomCloseTime);

            // Check if room should be closed (has disconnected players and time has passed)
            await CheckAndCloseRoomIfNeeded(roomKey, clients);

            // Clean up user mappings AFTER room operations
            if (room.IsMatchMaking)
            {
                MatchMakingRoomUsers.TryRemove(playerId, out _);
            }
            else
            {
                CodeRoomUsers.TryRemove(playerId, out _);
            }

            // Always remove from ActiveMatchmakingSessions to ensure player can start new matchmaking
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

                // Check if the room still exists and player is in it
                if (!Rooms.TryGetValue(roomKey, out Room? room) ||
                    !room.RoomPlayers.Any(rp => rp.PlayerId == playerId))
                {
                    inactiveSessions.Add(playerId);
                }
            }

            // Remove inactive sessions
            foreach (var playerId in inactiveSessions)
            {
                ActiveMatchmakingSessions.TryRemove(playerId, out _);
            }
        }
        
        public async Task ForceRemovePlayerFromAllRooms(string playerId, IHubCallerClients clients)
        {
            // Remove from all rooms (both matchmaking and code rooms)
            var roomsToRemove = Rooms.Where(r =>
                r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) ||
                r.Value.DisconnectedPlayers.ContainsKey(playerId)
            ).ToList();

            foreach (var room in roomsToRemove)
            {
                await CloseRoomAndKickAllPlayers(room.Key, clients, "Player started new matchmaking or left         room.");
            }

            // Remove from all mappings
            MatchMakingRoomUsers.TryRemove(playerId, out _);
            CodeRoomUsers.TryRemove(playerId, out _);
            ActiveMatchmakingSessions.TryRemove(playerId, out _);
        }
    }
}