using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using games;
using Models;
using Services;
using System.Security.Principal;
using System.Text.RegularExpressions;

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
            var user = await UserService.GetUserFromTokenAsync(jwtToken);
            await Groups.AddToGroupAsync(Context.ConnectionId, RoomService.CreateRoomKey(gameType, roomCode));
            await RoomService.JoinAsPlayerNotMatchMaking(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
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
                var roomKey = RoomService.CreateRoomKey(gameType, roomCode);
                var room = RoomService.GetRoomByKey(roomKey);

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

                // --- PATCH: Set room.RoomCloseTime so remaining player gets timer/banner ---
                if (room != null)
                {
                    room.RoomCloseTime = roomCloseTime;
                }

                // --- PATCH: Notify all remaining players in the room that a player left and set timer/banner ---
                await Clients.Group(roomKey).SendAsync(
                    "PlayerLeft",
                    playerId,
                    $"Player {playerId} left the room.",
                    roomCloseTime.ToString("o"),
                    room?.RoomPlayers.Count ?? 0
                );

                // --- PATCH: Actually close the room after timer expires if no players reconnect ---
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
                            RoomService.Rooms.TryRemove(roomKey, out _);
                        }
                        else
                        {
                            // If players reconnected, clear RoomCloseTime
                            updatedRoom.RoomCloseTime = null;
                            await Clients.Group(roomKey).SendAsync(
                                "PlayerReconnected",
                                playerId,
                                "Player reconnected, room will remain open."
                            );
                        }
                    }
                });

                // --- PATCH: Ensure leaving player is removed from room before timer logic ---
                await RoomService.HandlePlayerLeave(gameType, roomCode, playerId, Clients);
                Console.WriteLine($"JoinByCodeHub: HandlePlayerLeave completed for {playerId}");
            }
            else
            {
                Console.WriteLine("JoinByCodeHub: LeaveRoom called with missing parameters");
            }
        }

        // PATCH: OnDisconnectedAsync should trigger LeaveRoom logic for join-by-code rooms (like matchmaking)
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Get the player ID from the connection context
            var playerId = Context.GetHttpContext()?.Request.Query["playerId"].FirstOrDefault();
            var gameType = Context.GetHttpContext()?.Request.Query["gameType"].FirstOrDefault();
            var roomCode = Context.GetHttpContext()?.Request.Query["roomCode"].FirstOrDefault();

            // PATCH: Always send SetReturnBannerData to the disconnecting player for join-by-code rooms
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                // Defensive: Always send SetReturnBannerData, even if player already left
                await Clients.Client(Context.ConnectionId).SendAsync(
                    "SetReturnBannerData",
                    new {
                        gameType = gameType,
                        code = roomCode,
                        playerId = playerId,
                        isMatchmaking = false
                    },
                    DateTime.UtcNow.AddSeconds(30).ToString("o")
                );
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}