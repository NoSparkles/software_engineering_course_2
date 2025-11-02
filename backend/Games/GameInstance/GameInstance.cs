using Microsoft.AspNetCore.SignalR;
using Models.InMemoryModels;

namespace Games
{
    public abstract class GameInstance
    {
        public Dictionary<string, string> PlayerColors = new(); // PlayerId -> Color
        public string RoomCode { get; set; } = "";

        public abstract Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user);

        public string? GetPlayerColor(RoomUser rp)
        {
            if (rp.PlayerId != null && PlayerColors.ContainsKey(rp.PlayerId))
                return PlayerColors[rp.PlayerId];
            return null; // Not assigned (e.g., spectator or before assignment)
        }

        public void AssignPlayerColors(RoomUser rp1, RoomUser rp2)
        {
            if (rp1.PlayerId != null)
                PlayerColors[rp1.PlayerId] = "R";
            if (rp2.PlayerId != null)
                PlayerColors[rp2.PlayerId] = "Y";
        }

        // New: allow hubs/services to notify game of a reported win
        public abstract Task ReportWin(string playerId, IHubCallerClients clients);
    }
}