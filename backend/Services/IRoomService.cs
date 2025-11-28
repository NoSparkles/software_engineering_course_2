using System.Collections.Concurrent;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace Services
{
    public interface IRoomService
    {
        ConcurrentDictionary<string, Room> Rooms { get; set; } 
        ConcurrentDictionary<string, RoomUser> CodeRoomUsers { get; set; }
        ConcurrentDictionary<string, RoomUser> MatchMakingRoomUsers { get; set; }
        ConcurrentDictionary<string, string> ActiveMatchmakingSessions { get; set; }

        string CreateRoom(string gameType, bool isMatchMaking);
        Room? GetRoomByKey(string roomKey);  // Fixed: added ? and removed public
        bool RoomExists(string gameType, string roomCode);
        (bool exists, bool isMatchmaking) RoomExistsWithMatchmaking(string gameType, string roomCode);
        bool HasActiveMatchmakingSession(string playerId);
        Task ReportWin(string playerId, IHubCallerClients clients);
        Task JoinAsPlayerNotMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients);
        RoomUser? GetRoomUser(string roomKey, string playerId, User? user);  // Fixed: removed public
        Task JoinAsPlayerMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients);
        Task JoinAsSpectator(string gameType, string roomCode, string playerId, User? user = null);
        Task HandlePlayerDisconnect(string gameType, string roomCode, string playerId, IHubCallerClients clients);
        Task CheckAndCloseRoomIfNeeded(string roomKey, IHubCallerClients clients);
        Task CleanupExpiredRooms(IHubCallerClients clients);
        void ClearActiveMatchmakingSession(string playerId);
        Task CloseRoomAndKickAllPlayers(string roomKey, IHubCallerClients clients, string reason, string? excludePlayerId = null);
        Task StartRoomTimer(string roomKey, Room room, IHubCallerClients clients, string reason);
        void CleanupInactiveMatchmakingSessions();
        (string? gameType, string? roomCode, bool isMatchmaking) GetUserCurrentGame(string username);
        Task ForceRemovePlayerFromAllRooms(string playerId, IHubCallerClients clients);
        Task HandlePlayerLeave(string gameType, string roomCode, string playerId, IHubCallerClients clients); 
    }   
}