using Microsoft.AspNetCore.SignalR;

namespace games
{
    public class FourInARowGame : GameInstance
    {
        private Dictionary<string, string> playerColors = new();
        public void AssignPlayerColors(string player1Id, string player2Id)
        {
            playerColors[player1Id] = "R";
            playerColors[player2Id] = "Y";
        }

        public override Task HandleCommand(string playerId, string command, IHubCallerClients clients)
        {
            throw new NotImplementedException();
        }

        public string GetPlayerColor(string playerId)
        {
            return playerColors.ContainsKey(playerId) ? playerColors[playerId] : null;
        }
    }
}