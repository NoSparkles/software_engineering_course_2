using Games;

namespace Models.InMemoryModels
{
    public class Room : IDisposable
    {
        public GameInstance Game { get; set; }
        public bool IsMatchMaking { get; set; }
        public List<RoomUser> RoomPlayers { get; set; }
        public List<RoomUser> RoomSpectators { get; set; }
        public string Code { get; set; } = string.Empty;

        public bool GameStarted { get; set; }
        public Dictionary<string, RoomUser> DisconnectedPlayers { get; set; }
        public DateTime? RoomCloseTime { get; set; }
        public CancellationTokenSource? RoomTimerCancellation { get; set; }
        public Room(GameInstance game, bool isMatchMaking)
        {
            Game = game;
            IsMatchMaking = isMatchMaking;
            RoomPlayers = new List<RoomUser>();
            RoomSpectators = new List<RoomUser>();
            GameStarted = false;
            DisconnectedPlayers = new Dictionary<string, RoomUser>();
            RoomCloseTime = null;
            RoomTimerCancellation = null;
        }

        public static GameInstance GameTypeToGame(string gameType)
        {
            return gameType switch
                {
                    "rock-paper-scissors" => new RockPaperScissors(),
                    "four-in-a-row" => new FourInARowGame(),
                    "pair-matching" => new PairMatching(),
                    _ => throw new Exception("Unknown game type")
                };
        }

        public void Dispose()
        {
            RoomTimerCancellation?.Cancel();
            RoomTimerCancellation?.Dispose();

            foreach (var player in RoomPlayers)
            {
                var outgoing = player.User?.OutcomingInviteToGameRequests
                    .Where(inv => inv.RoomKey == Code)
                    .ToList();

                var incoming = player.User?.IncomingInviteToGameRequests
                    .Where(inv => inv.RoomKey == Code)
                    .ToList();

                if (outgoing is not null)
                {
                    foreach (var invite in outgoing)
                    {
                        player.User?.OutcomingInviteToGameRequests.Remove(invite);
                    }
                }
                
                if (incoming is not null)
                {
                    foreach (var invite in incoming)
                    {
                        player.User?.IncomingInviteToGameRequests.Remove(invite);
                    }                    
                }

            }
        }
    }
}