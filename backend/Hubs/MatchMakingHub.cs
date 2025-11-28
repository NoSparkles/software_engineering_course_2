using Services;
using Microsoft.AspNetCore.SignalR;
using Models.InMemoryModels;
using Extensions;
using Games;

namespace Hubs
{
    public class MatchMakingHub : Hub, IgameHub
    {
        private readonly IRoomService RoomService;
        private readonly IUserService UserService; // added

        public MatchMakingHub(IUserService userService, IRoomService roomService)
        {
            UserService = userService; // added
            RoomService = roomService;
        }

        public async Task ReportWin(string gameType, string roomCode, string playerId)
        {
            var roomKey = gameType.ToRoomKey(roomCode);

            // Check if room exists
            if (!RoomService.Rooms.ContainsKey(roomKey))
            {
                Console.WriteLine($"ReportWin failed: Room {roomKey} no longer exists");
                return;
            }

            var room = RoomService.GetRoomByKey(roomKey);
            if (room == null)
            {
                Console.WriteLine($"Room {roomKey} not found for ReportWin");
                return;
            }

            var game = room.Game;
            await game.ReportWin(playerId, Clients);

            // Determine winner/loser usernames (only if both are known)
            var winner = room.RoomPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            var loser = room.RoomPlayers.FirstOrDefault(p => p.PlayerId != null && p.PlayerId != playerId);

            var winnerUsername = winner?.Username;
            var loserUsername = loser?.Username;

            if (!string.IsNullOrWhiteSpace(winnerUsername) && !string.IsNullOrWhiteSpace(loserUsername))
            {
                var ok = await UserService.ApplyGameResultAsync(gameType, winnerUsername, loserUsername, isDraw: false);
                Console.WriteLine($"ApplyGameResultAsync({gameType}) -> {ok} for winner {winnerUsername}, loser {loserUsername}");
            }
            else
            {
                Console.WriteLine("ReportWin: Username(s) missing; skipping MMR update");
            }
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
            try
            {
                Console.WriteLine($"Join called with gameType: {gameType}, roomCode: {roomCode}, playerId: {playerId}");
                if (string.IsNullOrEmpty(gameType) || string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(playerId))
                {
                    Console.WriteLine("Join failed: Missing required parameters.");
                    throw new Exception("Missing required parameters for Join.");
                }
                var user = await UserService.GetUserFromTokenAsync(jwtToken);
                if (user == null)
                {
                    Console.WriteLine("User authentication failed in Join method");
                    throw new Exception("User authentication failed");
                }
                var roomKey = gameType.ToRoomKey(roomCode);
                if (!RoomService.Rooms.ContainsKey(roomKey))
                {
                    Console.WriteLine($"Join failed: Room {roomKey} does not exist.");
                    throw new Exception($"Room {roomKey} does not exist.");
                }
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
                var user = await UserService.GetUserFromTokenAsync(jwtToken);
                if (user == null)
                {
                    await Clients.Caller.SendAsync("UnauthorizedMatchmaking");
                    return null;
                }

                // Only cleanup if player is actually in any rooms
                var playerRooms = RoomService.Rooms.Where(r =>
                    r.Value.RoomPlayers.Any(p => p.PlayerId == playerId) ||
                    r.Value.DisconnectedPlayers.ContainsKey(playerId)
                ).ToList();

                if (playerRooms.Any())
                {
                    await RoomService.ForceRemovePlayerFromAllRooms(playerId, Clients);
                    await Task.Delay(50);
                }

                // Clean up any inactive matchmaking sessions
                RoomService.CleanupInactiveMatchmakingSessions();

                // Look for rooms with exactly 1 player (waiting for a second player)
                var availableRoom = RoomService.Rooms.FirstOrDefault(r =>
                    r.Key.StartsWith($"{gameType}:") &&
                    r.Value.RoomPlayers.Count == 1 &&
                    r.Value.IsMatchMaking &&
                    !r.Value.GameStarted);

                string roomCode;
                if (!string.IsNullOrEmpty(availableRoom.Key))
                {
                    // Double-check room still valid
                    if (RoomService.Rooms.TryGetValue(availableRoom.Key, out var foundRoom) &&
                        foundRoom.RoomPlayers.Count == 1 &&
                        foundRoom.IsMatchMaking &&
                        !foundRoom.GameStarted)
                    {
                        var parts = availableRoom.Key.Split(':');
                        roomCode = parts[1];
                        await Join(gameType, roomCode, playerId, jwtToken);
                        return roomCode;
                    }
                }

                // Create new room
                roomCode = RoomService.CreateRoom(gameType, true);
                await Join(gameType, roomCode, playerId, jwtToken);
                await Clients.Caller.SendAsync("WaitingForOpponent", roomCode);
                return roomCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in JoinMatchmaking: {ex.Message}");
                await Clients.Caller.SendAsync("MatchmakingError", ex.Message);
                return null;
            }
        }

        public async Task<object> RoomExistsWithMatchmaking(string gameType, string roomCode)
        {
            var result = RoomService.RoomExistsWithMatchmaking(gameType, roomCode);
            return await Task.FromResult(new { exists = result.exists, isMatchmaking = result.isMatchmaking });
        }

        public async Task JoinAsSpectator(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);
            if (!RoomService.Rooms.TryGetValue(roomKey, out var room))
            {
                await Clients.Caller.SendAsync("SpectatorJoinFailed", "Room does not exist");
                return;
            }

            Console.WriteLine($"MatchMakingHub.JoinAsSpectator called - conn:{Context.ConnectionId} room:{roomKey}");

            // create a spectator id based on connection id
            var spectatorId = "spec-" + Context.ConnectionId;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            await RoomService.JoinAsSpectator(gameType, roomCode, spectatorId, null);

            // send initial game state to caller using the same events player clients expect
            var game = room.Game;
            switch (game)
            {
                case FourInARowGame four:
                    var moveState = four.GetGameState();
                    await Clients.Caller.SendAsync("ReceiveMove", moveState);
                    await Clients.Caller.SendAsync("GameStateUpdate", moveState);
                    Console.WriteLine("MatchMakingHub: sent ReceiveMove+GameStateUpdate to spectator");
                    break;
                case PairMatching pair:
                    var boardState = pair.GetGameState();
                    await Clients.Caller.SendAsync("ReceiveBoard", boardState);
                    await Clients.Caller.SendAsync("GameStateUpdate", boardState);
                    Console.WriteLine("MatchMakingHub: sent ReceiveBoard+GameStateUpdate to spectator");
                    break;
                case RockPaperScissors rps:
                    var rpsState = rps.GetGameStatePublic();
                    await Clients.Caller.SendAsync("ReceiveRpsState", rpsState);
                    await Clients.Caller.SendAsync("GameStateUpdate", rpsState);
                    Console.WriteLine("MatchMakingHub: sent ReceiveRpsState+GameStateUpdate to spectator");
                    break;
            }

            await Clients.Group(roomKey).SendAsync("SpectatorJoined", spectatorId, "");
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

            var roomKey = gameType.ToRoomKey(roomCode);
            if (RoomService.Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"DeclineReconnection: Found room {roomKey} for player {playerId}");

                // For matchmaking rooms, close immediately when player declines reconnection
                Console.WriteLine($"Matchmaking room {roomKey} - closing immediately as player declined reconnection");

                // Close room and kick all remaining players
                await RoomService.CloseRoomAndKickAllPlayers(roomKey, Clients, "A player declined to reconnect");

                Console.WriteLine($"Room {roomKey} closed - player {playerId} declined reconnection");
            }
            else
            {
                Console.WriteLine($"DeclineReconnection: Room {roomKey} not found");
                // Clean up in case room was already closed
                RoomService.ClearActiveMatchmakingSession(playerId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"MatchMakingHub.OnDisconnectedAsync called - ConnectionId: {Context.ConnectionId}");

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

        public async Task LeaveRoom(string gameType, string roomCode, string playerId)
        {
            Console.WriteLine($"MatchMakingHub.LeaveRoom called - PlayerId: {playerId}, GameType: {gameType}, RoomCode: {roomCode}");

            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(gameType) || string.IsNullOrEmpty(roomCode))
            {
                Console.WriteLine("MatchMakingHub: LeaveRoom called with missing parameters");
                return;
            }

            var roomKey = gameType.ToRoomKey(roomCode);

            // Try to get the room safely
            if (!RoomService.Rooms.TryGetValue(roomKey, out Room? room))
            {
                Console.WriteLine($"Room {roomKey} not found for LeaveRoom");
                // Still remove from group even if room doesn't exist
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
                RoomService.ClearActiveMatchmakingSession(playerId);
                return;
            }

            Console.WriteLine($"MatchMakingHub: Processing LeaveRoom for {playerId} in {gameType}:{roomCode}");

            // Remove the leaving player from SignalR group FIRST
            // This prevents them from receiving RoomClosed event
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);
            Console.WriteLine($"Removed connection {Context.ConnectionId} from SignalR group {roomKey}");

            // For matchmaking rooms, close immediately and kick all players
            Console.WriteLine($"Matchmaking room {roomKey} - closing immediately as player pressed Leave Room");

            // Close room and kick all remaining players
            await RoomService.CloseRoomAndKickAllPlayers(roomKey, Clients, "A player left the matchmaking session", excludePlayerId: playerId);

            Console.WriteLine($"MatchMakingHub: Room closed and all players kicked for {playerId}");
        }
    }
    
}