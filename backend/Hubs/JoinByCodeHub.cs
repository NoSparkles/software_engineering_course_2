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
    }
}
