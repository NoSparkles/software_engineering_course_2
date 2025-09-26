using Services;
using Microsoft.AspNetCore.SignalR;
using Models;
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
                    Console.WriteLine($"Joining existing room {roomCode} for {user.Username} (playerId: {playerId})");
                    await Join(gameType, roomCode, playerId, jwtToken);
                    await Clients.Caller.SendAsync("MatchFound", roomCode);
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
    }
}