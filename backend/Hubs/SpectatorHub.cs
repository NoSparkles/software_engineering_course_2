using Microsoft.AspNetCore.SignalR;
using Services;
using Models;

namespace Hubs
{
    public class SpectatorHub : Hub
    {
        private readonly RoomService _roomService;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ConnectionToRoom = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(); // connectionId -> roomKey
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ConnectionToPlayer = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(); // connectionId -> playerId

        public SpectatorHub(RoomService roomService)
        {
            _roomService = roomService;
        }

        public async Task JoinAsSpectator(string gameType, string roomCode, string playerId)
        {
            // Add connection to the room group so spectators receive broadcasts
            var roomKey = _roomService.CreateRoomKey(gameType, roomCode);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);

            User? user = null;

            // Track mapping locally so we can cleanup on disconnect
            ConnectionToRoom[Context.ConnectionId] = roomKey;
            ConnectionToPlayer[Context.ConnectionId] = playerId;

            await _roomService.JoinAsSpectator(gameType, roomCode, playerId, user, Context.ConnectionId, Clients);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (ConnectionToRoom.TryRemove(Context.ConnectionId, out var roomKey))
            {
                ConnectionToPlayer.TryRemove(Context.ConnectionId, out var playerId);

                // roomKey is of form {gameType}:{ROOMCODE}
                var parts = roomKey.Split(':');
                if (parts.Length >= 2)
                {
                    var gameType = parts[0];
                    var roomCode = parts[1];
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        _roomService.RemoveSpectator(gameType, roomCode, playerId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}