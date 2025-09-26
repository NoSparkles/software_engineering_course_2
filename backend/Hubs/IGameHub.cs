using Microsoft.AspNetCore.SignalR;
using Models;

namespace Hubs
{
        public interface IgameHub
    {
        
        // previously named was MakeMove
        // async
        Task HandleCommand(string gameType, string roomCode, string playerId, string command, string jwtToken);

        // async
        Task Join(string gameType, string roomCode, string playerId, string jwtToken);
    }
}