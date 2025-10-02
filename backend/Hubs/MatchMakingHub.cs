using Services;
using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;
using System.Linq;

namespace Hubs
{
    public class MatchMakingHub : Hub, IgameHub
    {
        private readonly UserService UserService;
        private readonly RoomService RoomService;

        public MatchMakingHub(UserService userService, RoomService roomService)
        {
            UserService = userService;
            RoomService = roomService;
        }

        public async Task HandleCommand(string gameType, string roomCode, string playerId, string command, string jwtToken)
        {
            var roomKey = RoomService.CreateRoomKey(gameType, roomCode);
            var game = RoomService.GetRoomByKey(roomKey).Game;
            var user = await UserService.GetUserFromTokenAsync(jwtToken);
            var me = RoomService.GetRoomUser(roomKey, playerId, user);
            if (me is null)
            {
                return;
            }
            await game.HandleCommand(playerId, command, Clients, me);
        }

        public async Task Join(string gameType, string roomCode, string playerId, string jwtToken)
        {
            try
            {
                Console.WriteLine($"Join called with gameType: {gameType}, roomCode: {roomCode}, playerId: {playerId}");
                var user = await UserService.GetUserFromTokenAsync(jwtToken);
                if (user == null)
                {
                    Console.WriteLine("User authentication failed in Join method");
                    throw new Exception("User authentication failed");
                }
                var roomKey = RoomService.CreateRoomKey(gameType, roomCode);
                Console.WriteLine($"Ensuring connection is in group: {roomKey}");
                // Always add the connection to the group, even if already present
                await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
                Console.WriteLine($"Calling JoinAsPlayerMatchMaking");
                await RoomService.JoinAsPlayerMatchMaking(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
                Console.WriteLine($"JoinAsPlayerMatchMaking completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Join method: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<string?> JoinMatchmaking(string jwtToken, string gameType, string playerId)
        {
            try
            {
                Console.WriteLine($"JoinMatchmaking called with gameType: {gameType}, playerId: {playerId}");

                var user = await UserService.GetUserFromTokenAsync(jwtToken);
                if (user == null)
                {
                    Console.WriteLine("User authentication failed");
                    await Clients.Caller.SendAsync("UnauthorizedMatchmaking");
                    return null;
                }
                await RoomService.ForceRemovePlayerFromAllRooms(playerId, Clients);

                // First, find and close any room this player is currently in
                Room? oldRoom = null;
                string? oldRoomKey = null;

                if (RoomService.ActiveMatchmakingSessions.TryGetValue(playerId, out string? activeRoomKey))
                {
                    if (RoomService.Rooms.TryGetValue(activeRoomKey, out oldRoom))
                    {
                        oldRoomKey = activeRoomKey;
                        Console.WriteLine($"Player {playerId} found in ActiveMatchmakingSessions: {oldRoomKey}      ");
                    }
                }

                // If not found in ActiveMatchmakingSessions, check MatchMakingRoomUsers
                if (oldRoom == null && RoomService.MatchMakingRoomUsers.TryGetValue(playerId, out RoomUser?         roomUser))
                {
                    // Find the room that contains this player
                    foreach (var room in RoomService.Rooms)
                    {
                        if (room.Value.RoomPlayers.Any(p => p.PlayerId == playerId) || 
                            room.Value.DisconnectedPlayers.ContainsKey(playerId))
                        {
                            oldRoom = room.Value;
                            oldRoomKey = room.Key;
                            Console.WriteLine($"Player {playerId} found in room {oldRoomKey} via        MatchMakingRoomUsers");
                            break;
                        }
                    }
                }

                // If still not found, search all rooms for the player
                if (oldRoom == null)
                {
                    foreach (var room in RoomService.Rooms)
                    {
                        if (room.Value.RoomPlayers.Any(p => p.PlayerId == playerId) || 
                            room.Value.DisconnectedPlayers.ContainsKey(playerId))
                        {
                            oldRoom = room.Value;
                            oldRoomKey = room.Key;
                            Console.WriteLine($"Player {playerId} found in room {oldRoomKey} via room search");
                            break;
                        }
                    }
                }

                // Close all rooms where the player is present (connected or disconnected)
                var roomsToClose = RoomService.Rooms.Where(r =>
                    r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) ||
                    r.Value.DisconnectedPlayers.ContainsKey(playerId)
                ).ToList();
                foreach (var room in roomsToClose)
                {
                    var parts = room.Key.Split(':');
                    var closeGameType = parts[0];
                    var closeRoomCode = parts[1];
                    // Call DeclineReconnection for every player in the room
                    var allPlayers = room.Value.RoomPlayers.Select(p => p.PlayerId)
                        .Concat(room.Value.DisconnectedPlayers.Keys)
                        .Distinct().ToList();
                    foreach (var pid in allPlayers)
                    {
                        Console.WriteLine($"Closing room {room.Key} via DeclineReconnection for player {pid}");
                        await DeclineReconnection(pid, closeGameType, closeRoomCode);
                    }
                }


                Console.WriteLine($"Looking for rooms that might be waiting for player {playerId}");
                var roomsWaitingForPlayer = RoomService.Rooms.Where(r => 
                    r.Value.DisconnectedPlayers.ContainsKey(playerId) && 
                    r.Value.IsMatchMaking).ToList();

                foreach (var room in roomsWaitingForPlayer)
                {
                    Console.WriteLine($"Found room {room.Key} waiting for player {playerId}, closing it");
                    await RoomService.CloseRoomAndKickAllPlayers(room.Key, Clients, "Player joined new      matchmaking - old room closed");
                }


                Console.WriteLine($"Looking for active rooms that should be closed for player {playerId}");
                var activeRoomsWithPlayer = RoomService.Rooms.Where(r => 
                    r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) && 
                    r.Value.IsMatchMaking &&
                    r.Value.RoomPlayers.Count > 1).ToList(); // Only close rooms with multiple players

                foreach (var room in activeRoomsWithPlayer)
                {
                    Console.WriteLine($"Found active room {room.Key} with player {playerId}, closing it");
                    await RoomService.CloseRoomAndKickAllPlayers(room.Key, Clients, "Player joined new      matchmaking - old room closed");
                }

                Console.WriteLine($"Authenticated matchmaking request from {user.Username} (playerId:       {playerId}) for {gameType}");

                // Clean up any inactive matchmaking sessions first
                RoomService.CleanupInactiveMatchmakingSessions();

                // First, look for rooms with exactly 1 player (waiting for a second player)
                var availableRoom = RoomService.Rooms.FirstOrDefault(r => 
                    r.Key.StartsWith($"{gameType}:") && 
                    r.Value.RoomPlayers.Count == 1 && 
                    r.Value.IsMatchMaking);

                string roomCode;
                if (!string.IsNullOrEmpty(availableRoom.Key))
                {
                    // Join existing room with 1 player
                    var parts = availableRoom.Key.Split(':');
                    roomCode = parts[1];
                    var roomKey = RoomService.CreateRoomKey(gameType, roomCode);
                    Console.WriteLine($"Joining existing room {roomCode} for {user.Username} (playerId:         {playerId})");
                    await Join(gameType, roomCode, playerId, jwtToken);
                    // MatchFound event will be sent from JoinAsPlayerMatchMaking when game starts
                    return roomCode;
                }
                else
                {
                    // Create new room
                    roomCode = RoomService.CreateRoom(gameType, true);
                    Console.WriteLine($"Created new room {roomCode} for {user.Username} (playerId: {playerId})      ");
                    await Join(gameType, roomCode, playerId, jwtToken);
                    await Clients.Caller.SendAsync("WaitingForOpponent", roomCode);
                    return roomCode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in JoinMatchmaking: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("MatchmakingError", ex.Message);
                return null;
            }
        }

        public async Task<object> RoomExistsWithMatchmaking(string gameType, string roomCode)
        {
            var result = RoomService.RoomExistsWithMatchmaking(gameType, roomCode);
            return await Task.FromResult(new { exists = result.exists, isMatchmaking = result.isMatchmaking });
        }

        public async Task EndMatchmakingSession(string playerId)
        {
            Console.WriteLine($"EndMatchmakingSession called for player {playerId}");
            Console.WriteLine($"ActiveMatchmakingSessions count: {RoomService.ActiveMatchmakingSessions.Count}");
            Console.WriteLine($"ActiveMatchmakingSessions keys: {string.Join(", ", RoomService.ActiveMatchmakingSessions.Keys)}");
            
            // Find the room this player is in
            var playerRoom = RoomService.ActiveMatchmakingSessions.FirstOrDefault(kvp => kvp.Key == playerId);
            if (playerRoom.Value != null)
            {
                var roomKey = playerRoom.Value;
                Console.WriteLine($"EndMatchmakingSession: Found room {roomKey} for player {playerId}");
                
                // Close the room and kick all players immediately
                await RoomService.CloseRoomAndKickAllPlayers(roomKey, Clients, "Matchmaking session ended by a player");
            }
            else
            {
                Console.WriteLine($"EndMatchmakingSession: No active session found for player {playerId}");
                // Player doesn't have an active session, just clear it
                RoomService.ClearActiveMatchmakingSession(playerId);
                await Clients.Caller.SendAsync("MatchmakingSessionEnded", "No active matchmaking session found.");
            }
        }

        public async Task DeclineReconnection(string playerId, string gameType, string roomCode)
        {
            Console.WriteLine($"DeclineReconnection called for player {playerId}, gameType: {gameType}, roomCode: {roomCode}");

            var roomKey = RoomService.CreateRoomKey(gameType, roomCode);
            if (RoomService.Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"DeclineReconnection: Found room {roomKey} for player {playerId}");
                // Restore previous behavior: cancel timers, clear state, notify, and remove room
                room.RoomTimerCancellation?.Cancel();
                room.RoomTimerCancellation = null;
                room.RoomCloseTime = null;
                room.DisconnectedPlayers.Clear();
                await Clients.Group(roomKey).SendAsync("RoomClosed", "A player declined to reconnect. Room closed immediately.");
                Console.WriteLine($"Sent immediate RoomClosed event to group {roomKey}");
                foreach (var roomPlayer in room.RoomPlayers)
                {
                    Console.WriteLine($"Cleaning up mappings for player {roomPlayer.PlayerId}");
                    if (room.IsMatchMaking)
                    {
                        RoomService.MatchMakingRoomUsers.TryRemove(roomPlayer.PlayerId, out _);
                        RoomService.ActiveMatchmakingSessions.TryRemove(roomPlayer.PlayerId, out _);
                    }
                    else
                    {
                        RoomService.CodeRoomUsers.TryRemove(roomPlayer.PlayerId, out _);
                    }
                }
                RoomService.Rooms.TryRemove(roomKey, out _);
                Console.WriteLine($"Room {roomKey} closed and all players kicked immediately");
            }
            else
            {
                Console.WriteLine($"DeclineReconnection: Room {roomKey} not found");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Get the player ID from the connection context
            var playerId = Context.GetHttpContext()?.Request.Query["playerId"].FirstOrDefault();
            var gameType = Context.GetHttpContext()?.Request.Query["gameType"].FirstOrDefault();
            var roomCode = Context.GetHttpContext()?.Request.Query["roomCode"].FirstOrDefault();

            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                await RoomService.HandlePlayerDisconnect(gameType, roomCode, playerId, Clients);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task LeaveRoom(string gameType, string roomCode, string playerId)
        {
            Console.WriteLine($"MatchMakingHub.LeaveRoom called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");


            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine($"MatchMakingHub: Calling HandlePlayerLeave for {playerId} in {gameType}:{roomCode}");
                await RoomService.HandlePlayerLeave(gameType, roomCode, playerId, Clients);
                Console.WriteLine($"MatchMakingHub: HandlePlayerLeave completed for {playerId}");
            }
            else
            {
                Console.WriteLine("MatchMakingHub: LeaveRoom called with missing parameters");
            }
        }
    }
}