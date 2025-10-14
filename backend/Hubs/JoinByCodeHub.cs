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
            
            // Check if room exists
            if (!RoomService.Rooms.ContainsKey(roomKey))
            {
                Console.WriteLine($"HandleCommand failed: Room {roomKey} no longer exists");
                return;
            }
            
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
            var roomKey = gameType.ToRoomKey(roomCode);
            
            // Check if room exists before trying to join
            if (!RoomService.Rooms.ContainsKey(roomKey))
            {
                Console.WriteLine($"Join failed: Room {roomKey} does not exist");
                await Clients.Caller.SendAsync("JoinFailed", "Room no longer exists. It may have been closed.");
                return;
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            await RoomService.JoinAsPlayerNotMatchMaking(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
            
            // Small delay to ensure room setup is complete
            await Task.Delay(500);
        }

        // Allow joining as a spectator through the same hub (SessionRoom uses this)
        public async Task JoinAsSpectator(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!RoomService.Rooms.TryGetValue(roomKey, out var room))
            {
                await Clients.Caller.SendAsync("SpectatorJoinFailed", "Room does not exist");
                return;
            }

            Console.WriteLine($"JoinByCodeHub.JoinAsSpectator called - conn:{Context.ConnectionId} room:{roomKey}");

            // create a spectator id based on connection id
            var spectatorId = "spec-" + Context.ConnectionId;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            await RoomService.JoinAsSpectator(gameType, roomCode, spectatorId, null, Context.ConnectionId);

            // send initial game state to caller using the same events player clients expect
            var game = room.Game;
            switch (game)
            {
                case FourInARowGame four:
                    var moveState = four.GetGameState();
                    await Clients.Caller.SendAsync("ReceiveMove", moveState);
                    await Clients.Caller.SendAsync("GameStateUpdate", moveState);
                    Console.WriteLine("JoinByCodeHub: sent ReceiveMove+GameStateUpdate to spectator");
                    break;
                case PairMatching pair:
                    var boardState = pair.GetGameState();
                    await Clients.Caller.SendAsync("ReceiveBoard", boardState);
                    await Clients.Caller.SendAsync("GameStateUpdate", boardState);
                    Console.WriteLine("JoinByCodeHub: sent ReceiveBoard+GameStateUpdate to spectator");
                    break;
                case RockPaperScissors rps:
                    var rpsState = rps.GetGameStatePublic();
                    await Clients.Caller.SendAsync("ReceiveRpsState", rpsState);
                    await Clients.Caller.SendAsync("GameStateUpdate", rpsState);
                    Console.WriteLine("JoinByCodeHub: sent ReceiveRpsState+GameStateUpdate to spectator");
                    break;
            }

            await Clients.Group(roomKey).SendAsync("SpectatorJoined", spectatorId, "");
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

            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(gameType) || string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine("JoinByCodeHub: LeaveRoom called with missing parameters");
                return;
            }

            var roomKey = gameType.ToRoomKey(roomCode);
            
            // Try to get the room safely
            if (!RoomService.Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"Room {roomKey} not found for LeaveRoom");
                // Still remove from group even if room doesn't exist
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
                RoomService.CodeRoomUsers.TryRemove(playerId, out _);
                return;
            }

            Console.WriteLine($"JoinByCodeHub: Processing LeaveRoom for {playerId} in {gameType}:{roomCode}");

            // Remove the leaving player from SignalR group FIRST
            // This prevents them from receiving RoomClosed event
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            Console.WriteLine($"Removed connection {Context.ConnectionId} from SignalR group {roomKey}");

            // Close room immediately and kick all players
            Console.WriteLine($"Code-based room {roomKey} - closing immediately as player pressed Leave Room");
            
            // Close room and kick all remaining players
            await RoomService.CloseRoomAndKickAllPlayers(roomKey, Clients, "A player left the room", excludePlayerId: playerId);

            Console.WriteLine($"JoinByCodeHub: Room closed and all players kicked for {playerId}");
        }

        public async Task DeclineReconnection(string playerId, string gameType, string roomCode)
        {
            Console.WriteLine($"JoinByCodeHub.DeclineReconnection called for player {playerId}, gameType: {gameType}, roomCode: {roomCode}");

            var roomKey = gameType.ToRoomKey(roomCode);
            if (RoomService.Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"DeclineReconnection: Found room {roomKey} for player {playerId}");
                
                // Close room immediately when player declines reconnection
                Console.WriteLine($"Code-based room {roomKey} - closing immediately as player declined reconnection");
                
                // Close room and kick all remaining players
                await RoomService.CloseRoomAndKickAllPlayers(roomKey, Clients, "A player declined to reconnect");
                
                Console.WriteLine($"Room {roomKey} closed - player {playerId} declined reconnection");
            }
            else
            {
                Console.WriteLine($"DeclineReconnection: Room {roomKey} not found");
                // Clean up in case room was already closed
                RoomService.CodeRoomUsers.TryRemove(playerId, out _);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"JoinByCodeHub.OnDisconnectedAsync called - ConnectionId: {Context.ConnectionId}");
            
            var playerId = Context.GetHttpContext()?.Request.Query["playerId"].FirstOrDefault();
            var gameType = Context.GetHttpContext()?.Request.Query["gameType"].FirstOrDefault();
            var roomCode = Context.GetHttpContext()?.Request.Query["roomCode"].FirstOrDefault();

            Console.WriteLine($"OnDisconnectedAsync - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");

            // OnDisconnectedAsync handles implicit disconnection (navigation, connection drop)
            // This should mark player as disconnected but allow reconnection (timer)
            // NOT the same as explicit LeaveRoom (button press)
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(gameType) && !string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine($"Calling HandlePlayerDisconnect for {playerId}");
                await RoomService.HandlePlayerDisconnect(gameType, roomCode, playerId, Clients);
            }
            else
            {
                Console.WriteLine("OnDisconnectedAsync: Missing parameters, not calling HandlePlayerDisconnect");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}