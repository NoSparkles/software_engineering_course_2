using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Games;
using Models;
using Models.InMemoryModels;
using Services;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Extensions;

namespace Hubs
{
    public class JoinByCodeHub : Hub, IgameHub
    {
        private readonly UserService UserService;
        private readonly RoomService RoomService;

        public JoinByCodeHub(UserService userService, RoomService roomService)
        {
            UserService = userService;
            RoomService = roomService;
        }


        public async Task HandleCommand(string gameType, string roomCode, string playerId, string command, string jwtToken)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            var room = RoomService.GetRoomByKey(roomKey);
            if (room == null)
            {
                Console.WriteLine($"Room {roomKey} not found for HandleCommand");
                return;
            }
            var game = room.Game;
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
            var user = await UserService.GetUserFromTokenAsync(jwtToken);
            await Groups.AddToGroupAsync(Context.ConnectionId, gameType.ToRoomKey(roomCode));
            await RoomService.JoinAsPlayerNotMatchMaking(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
            
            // Small delay to ensure room setup is complete
            await Task.Delay(500);
        }

        public Task<string> CreateRoom(string gameType, bool isMatchmaking)
        {
            return Task.FromResult(RoomService.CreateRoom(gameType, isMatchmaking));
        }

        public async Task<bool> RoomExists(string gameType, string roomCode)
        {
            return await Task.FromResult(RoomService.RoomExists(gameType, roomCode));
        }

        public async Task<object> RoomExistsWithMatchmaking(string gameType, string roomCode)
        {
            var result = RoomService.RoomExistsWithMatchmaking(gameType, roomCode);
            return await Task.FromResult(new { exists = result.exists, isMatchmaking = result.isMatchmaking });
        }

        public async Task LeaveRoom(string gameType, string roomCode, string playerId)
        {
            Console.WriteLine($"JoinByCodeHub.LeaveRoom called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");

            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine($"JoinByCodeHub: Calling HandlePlayerLeave for {playerId} in {gameType}:{roomCode}");
                var roomKey = gameType.ToRoomKey(roomCode);
                var room = RoomService.GetRoomByKey(roomKey);
                if (room == null)
                {
                    Console.WriteLine($"Room {roomKey} not found for LeaveRoom");
                    return;
                }

                // Calculate roomCloseTime (30 seconds from now)
                var roomCloseTime = DateTime.UtcNow.AddSeconds(30);

                // --- PATCH: Always send SetReturnBannerData and PlayerLeftRoom to the leaving player ---
                await Clients.Caller.SendAsync(
                    "SetReturnBannerData",
                    new {
                        gameType = gameType,
                        code = roomCode,
                        playerId = playerId,
                        isMatchmaking = false
                    },
                    roomCloseTime.ToString("o")
                );
                await Clients.Caller.SendAsync(
                    "PlayerLeftRoom",
                    "You left the room. You can return for 30 seconds.",
                    roomCloseTime.ToString("o"),
                    room?.RoomPlayers.Count ?? 1
                );

                if (room != null)
                {
                    room.RoomCloseTime = roomCloseTime;
                }

            
                var remainingPlayers = room?.RoomPlayers?.Where(p => p.PlayerId != playerId).Select(p => p.PlayerId).ToList() ?? new List<string>();
                await Clients.Group(roomKey).SendAsync("RoomPlayersUpdate", remainingPlayers);

            
                await Clients.Group(roomKey).SendAsync(
                    "PlayerLeft",
                    playerId,
                    $"Player {playerId} left the room.",
                    roomCloseTime.ToString("o"),
                    room?.RoomPlayers.Count ?? 0
                );

         
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(31));
                    var updatedRoom = RoomService.GetRoomByKey(roomKey);
                    if (updatedRoom != null && updatedRoom.RoomCloseTime.HasValue)
                    {
                        // If no players reconnected, close the room
                        if (updatedRoom.RoomPlayers.Count == 0)
                        {
                            await Clients.Group(roomKey).SendAsync(
                                "RoomClosed",
                                "Room closed - timer expired and all players left.",
                                roomCode
                            );
                            if (RoomService.Rooms.TryRemove(roomKey, out Room? removedRoom))
                            {
                                removedRoom?.Dispose();
                            }
                        }
                        else
                        {
              
                            updatedRoom.RoomCloseTime = null;
                            await Clients.Group(roomKey).SendAsync(
                                "PlayerReconnected",
                                playerId,
                                "Player reconnected, room will remain open."
                            );
                        }
                    }
                });

        
                await RoomService.HandlePlayerLeave(gameType, roomCode, playerId, Clients);
                Console.WriteLine($"JoinByCodeHub: HandlePlayerLeave completed for {playerId}");
            }
            else
            {
                Console.WriteLine("JoinByCodeHub: LeaveRoom called with missing parameters");
            }
        }

        public async Task DeclineReconnection(string playerId, string gameType, string roomCode)
        {
            Console.WriteLine($"JoinByCodeHub.DeclineReconnection called for player {playerId}, gameType: {gameType}, roomCode: {roomCode}");

            var roomKey = gameType.ToRoomKey(roomCode);
            if (RoomService.Rooms.TryGetValue(roomKey, out Room room))
            {
                Console.WriteLine($"DeclineReconnection: Found room {roomKey} for player {playerId}");
                
         
                room.DisconnectedPlayers.Remove(playerId);
                
        
                var remainingPlayers = room.RoomPlayers?.Select(p => p.PlayerId).ToList() ?? new List<string>();
                
             
                bool allPlayersDisconnected = room.RoomPlayers.All(rp => room.DisconnectedPlayers.ContainsKey(rp.PlayerId));  //Iterating through collection
                
                if (allPlayersDisconnected)
                {
                    // All players disconnected, close room immediately
                    await Clients.Group(roomKey).SendAsync("RoomClosed", "All players declined to reconnect. Room closed.");
                    if (RoomService.Rooms.TryRemove(roomKey, out Room? removedRoom))
                    {
                        removedRoom?.Dispose();
                    }
                    Console.WriteLine($"Room {roomKey} closed - all players declined reconnection");
                    return;
                }
                
                // Start timer for remaining players
                await RoomService.StartRoomTimer(roomKey, room, Clients, "Player declined reconnection");
                
                // Send events to remaining players
                await Clients.Group(roomKey).SendAsync("RoomPlayersUpdate", remainingPlayers);
                await Clients.Group(roomKey).SendAsync("PlayerDeclinedReconnection", playerId, "A player declined to reconnect. Room will close in 30 seconds.");
                
                Console.WriteLine($"Room {roomKey} timer started - player {playerId} declined reconnection");
            }
            else
            {
                Console.WriteLine($"DeclineReconnection: Room {roomKey} not found");
            }
        }

        // PATCH: OnDisconnectedAsync should trigger LeaveRoom logic for join-by-code rooms (like matchmaking)
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var playerId = Context.GetHttpContext()?.Request.Query["playerId"].FirstOrDefault();
            var gameType = Context.GetHttpContext()?.Request.Query["gameType"].FirstOrDefault();
            var roomCode = Context.GetHttpContext()?.Request.Query["roomCode"].FirstOrDefault();

            // Always call LeaveRoom for join-by-code rooms on disconnect, regardless of navigation target
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                await LeaveRoom(gameType, roomCode, playerId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}