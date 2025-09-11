using Microsoft.AspNetCore.SignalR;

namespace games
{
    public class PairMatching : GameInstance
    {
        public override Task HandleCommand(string playerId, string command, IHubCallerClients clients)
        {
            throw new NotImplementedException();
        }
    }
}