using Services;
using Microsoft.AspNetCore.SignalR;
using Models;

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
            await game.HandleCommand(playerId, command, Clients, me);
        }

        public async Task Join(string gameType, string roomCode, string playerId, string jwtToken)
        {
            var user = await UserService.GetUserFromTokenAsync(jwtToken);
            await Groups.AddToGroupAsync(Context.ConnectionId, RoomService.CreateRoomKey(gameType, roomCode));
            await RoomService.JoinAsPlayerMatchMaking(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
        }

        public async Task<string?> JoinMatchmaking(string jwtToken, string gameType)
        {
            var user = await UserService.GetUserFromTokenAsync(jwtToken);
            if (user == null)
            {
                await Clients.Caller.SendAsync("UnauthorizedMatchmaking");
                return null;
            }

            var availableRoom = RoomService.Rooms.FirstOrDefault(r => r.Key.StartsWith($"{gameType}") && r.Value.RoomPlayers.Count < 2);

            string roomCode;
            if (!string.IsNullOrEmpty(availableRoom.Key))
            {
                var parts = availableRoom.Key.Split(':');
                roomCode = parts[1];
                await Join(gameType, roomCode, user.Username, jwtToken);
                await Clients.Caller.SendAsync("MatchFound");
                return roomCode;
            }
            else
            {
                roomCode = RoomService.CreateRoom(gameType, true);
                await Join(gameType, roomCode, user.Username, jwtToken);
                await Clients.Caller.SendAsync("WaitingForOpponent");
                return roomCode;
            }
            
        }
    }
}