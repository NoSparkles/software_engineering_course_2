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
                    return true;
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
            var roomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            // Check if this is a reconnection
            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);
                if (room.DisconnectedPlayers.Count == 0)
                {
                    room.RoomCloseTime = null;
                }
                await clients.Group(roomKey).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
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

        public RoomUser? GetRoomUser(string roomKey, string playerId, User? user) {
            var roomPlayers = GetRoomByKey(roomKey).RoomPlayers;
            return roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);
        }

         public async Task JoinAsPlayerMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
         {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var room = GetRoomByKey(roomKey);
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;
            var roomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);
            
            // Check if this is a reconnection
            if (room.DisconnectedPlayers.ContainsKey(playerId))
            {
                room.DisconnectedPlayers.Remove(playerId);
                if (room.DisconnectedPlayers.Count == 0)
                {
                    room.RoomCloseTime = null;
                }
                await clients.Group(roomKey).SendAsync("PlayerReconnected", playerId, "Player reconnected successfully!");
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

                var playerIdToColor = new Dictionary<string, string>();
                foreach (var rp in room.RoomPlayers)
                {
                    playerIdToColor[rp.PlayerId] = game.GetPlayerColor(rp);
                }

                // Send MatchFound, StartGame and SetPlayerColor to all players in the room
                if (room.IsMatchMaking)
                {
                    await clients.Group(roomKey).SendAsync("MatchFound", roomCode);
                }
                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
                await clients.Group(roomKey).SendAsync("SetPlayerColor", playerIdToColor);
                Console.WriteLine($"Sent MatchFound, StartGame and SetPlayerColor to group {roomKey}");
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

        public void JoinAsSpectator(string gameType, string roomCode, string playerId, User? user, string connectionId)
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
            
            // Notify all players in the room about the disconnection
            await clients.Group(roomKey).SendAsync("PlayerDisconnected", playerId, "Player disconnected. Room will close in 30 seconds if they don't reconnect.", room.RoomCloseTime);
            
            // Set room close time if not already set
            if (room.RoomCloseTime == null)
            {
                room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
                
                // Start a timer to close the room if player doesn't reconnect
                _ = Task.Run(async () =>
                {
                    await Task.Delay(30000); // Wait 30 seconds
                    await CheckAndCloseRoomIfNeeded(roomKey, clients);
                });
            }
        }


        public async Task CheckAndCloseRoomIfNeeded(string roomKey, IHubCallerClients clients)
        {
            if (!Rooms.TryGetValue(roomKey, out Room? room))
                return;

            // Check if room should be closed (has disconnected players and time has passed)
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
    }
}