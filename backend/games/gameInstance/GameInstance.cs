using Microsoft.AspNetCore.SignalR;

namespace games
{
    public abstract class GameInstance
    {
        public abstract Task HandleCommand(string playerId, string command, IHubCallerClients clients);
        
    }
}