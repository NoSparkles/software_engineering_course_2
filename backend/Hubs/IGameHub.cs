using Microsoft.AspNetCore.SignalR;
using Models;

namespace Hubs
{
        public interface IgameHubInterface
    {
        // previously named was MakeMove
        // async
        Task HandleCommand(string gameType, string roomCode, string command, string playerId, User? user);

        // async
        Task Join(string gameType, string roomCode, string playerId, User? user);
    }
}