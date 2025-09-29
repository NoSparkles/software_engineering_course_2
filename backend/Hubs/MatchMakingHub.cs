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
                Console.WriteLine($"Adding to group: {roomKey}");
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

                // Check if player already has an active matchmaking session
                if (RoomService.HasActiveMatchmakingSession(playerId))
                {
                    await Clients.Caller.SendAsync("MatchmakingError", "You already have an active matchmaking session. Please finish your current game first.");
                    return null;
                }

                Console.WriteLine($"Authenticated matchmaking request from {user.Username} (playerId: {playerId}) for {gameType}");

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
                    Console.WriteLine($"Joining existing room {roomCode} for {user.Username} (playerId: {playerId})");
                    await Join(gameType, roomCode, playerId, jwtToken);
                    // MatchFound event will be sent from JoinAsPlayerMatchMaking when game starts
                    return roomCode;
                }
                else
                {
                    // Create new room
                    roomCode = RoomService.CreateRoom(gameType, true);
                    Console.WriteLine($"Created new room {roomCode} for {user.Username} (playerId: {playerId})");
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
            // Find the room this player is in
            var playerRoom = RoomService.ActiveMatchmakingSessions.FirstOrDefault(kvp => kvp.Key == playerId);
            if (playerRoom.Value != null)
            {
                var roomKey = playerRoom.Value;
                if (RoomService.Rooms.TryGetValue(roomKey, out Room? room))
                {
                    // Clear active sessions for all players in the room
                    foreach (var roomPlayer in room.RoomPlayers)
                    {
                        RoomService.ClearActiveMatchmakingSession(roomPlayer.PlayerId);
                    }
                    
                    // Notify all players in the room that the session was ended
                    await Clients.Group(roomKey).SendAsync("MatchmakingSessionEnded", "Matchmaking session ended by a player.");
                    
                    // Remove the room
                    RoomService.Rooms.TryRemove(roomKey, out _);
                    
                    // Clean up user mappings
                    foreach (var player in room.RoomPlayers)
                    {
                        RoomService.MatchMakingRoomUsers.TryRemove(player.PlayerId, out _);
                    }
                }
            }
            else
            {
                // Player doesn't have an active session, just clear it
                RoomService.ClearActiveMatchmakingSession(playerId);
                await Clients.Caller.SendAsync("MatchmakingSessionEnded", "No active matchmaking session found.");
            }
        }

        public async Task DeclineReconnection(string playerId)
        {
            // Find the room this player is in
            var playerRoom = RoomService.ActiveMatchmakingSessions.FirstOrDefault(kvp => kvp.Key == playerId);
            if (playerRoom.Value != null)
            {
                var roomKey = playerRoom.Value;
                if (RoomService.Rooms.TryGetValue(roomKey, out Room? room))
                {
                    // Remove this player from the room
                    room.RoomPlayers.RemoveAll(p => p.PlayerId == playerId);
                    RoomService.ClearActiveMatchmakingSession(playerId);
                    RoomService.MatchMakingRoomUsers.TryRemove(playerId, out _);
                    
                    // If no players left, remove the room
                    if (room.RoomPlayers.Count == 0)
                    {
                        RoomService.Rooms.TryRemove(roomKey, out _);
                    }
                    else
                    {
                        // Notify remaining players that a player declined reconnection
                        await Clients.Group(roomKey).SendAsync("PlayerDeclinedReconnection", playerId, "A player declined to reconnect. Room will close in 30 seconds.");
                        
                        // Set room close time for remaining players
                        room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
                        
                        // Start timer to close room
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(30000);
                            await RoomService.CheckAndCloseRoomIfNeeded(roomKey, Clients);
                        });
                    }
                }
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
    }
}