using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace Games
{
    public abstract class GameInstance
    {
        public Dictionary<string, string> PlayerColors = new(); // PlayerId -> Color
        public string RoomCode { get; set; } = "";

        public abstract Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user);

        public string GetPlayerColor(RoomUser rp)
        {
            return PlayerColors[rp.PlayerId];
        }

        public void AssignPlayerColors(RoomUser rp1, RoomUser rp2)
        {
            PlayerColors[rp1.PlayerId] = "R";
            PlayerColors[rp2.PlayerId] = "Y";
        }
    }
}