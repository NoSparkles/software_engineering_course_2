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
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<SpectatorHub> _spectatorHubContext;

        public JoinByCodeHub(UserService userService, RoomService roomService, Microsoft.AspNetCore.SignalR.IHubContext<SpectatorHub> spectatorHubContext)
        {
            UserService = userService;
            RoomService = roomService;
            _spectatorHubContext = spectatorHubContext;
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

            // Forward updated game state to any spectators connected to SpectatorHub for this room
            switch (game)
            {
                case games.FourInARowGame four:
                    await _spectatorHubContext.Clients.Group(roomKey).SendAsync("ReceiveMove", four.GetGameState());
                    break;
                case games.PairMatching pair:
                    await _spectatorHubContext.Clients.Group(roomKey).SendAsync("ReceiveBoard", pair.GetGameState());
                    break;
                case games.RockPaperScissors rps:
                    await _spectatorHubContext.Clients.Group(roomKey).SendAsync("ReceiveRpsState", rps.GetGameStatePublic());
                    break;
                default:
                    break;
            }
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
            Console.WriteLine($"JoinByCodeHub.LeaveRoom called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");

            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine($"JoinByCodeHub: Calling HandlePlayerLeave for {playerId} in {gameType}:{roomCode}");
                await RoomService.HandlePlayerLeave(gameType, roomCode, playerId, Clients);
                Console.WriteLine($"JoinByCodeHub: HandlePlayerLeave completed for {playerId}");
            }
            else
            {
                Console.WriteLine("JoinByCodeHub: LeaveRoom called with missing parameters");
            }
        }
    }
}
