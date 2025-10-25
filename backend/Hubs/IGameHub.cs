namespace Hubs
{
        public interface IgameHub
    {
        
        Task HandleCommand(string gameType, string roomCode, string playerId, string command, string jwtToken);

        Task Join(string gameType, string roomCode, string playerId, string jwtToken);
    }
}