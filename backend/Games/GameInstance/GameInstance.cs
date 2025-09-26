using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace games
{
    public abstract class GameInstance
    {
        public Dictionary<RoomUser, string> PlayerColors = new();
        public string RoomCode { get; set; } = "";

        public abstract Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user);

        public string GetPlayerColor(RoomUser rp)
        {
            return PlayerColors[rp];
        }

        public void AssignPlayerColors(RoomUser rp1, RoomUser rp2)
        {
            PlayerColors[rp1] = "R";
            PlayerColors[rp2] = "Y";
        }

        
    }
}