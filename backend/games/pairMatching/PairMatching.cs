using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration.CommandLine;

namespace games
{
    public class PairMatching : GameInstance
    {
        public Dictionary<string, string> playerColors = new();
        private Card[,] Board { get; set; } = new Card[6, 3];
        public string CurrentPlayerColor { get; private set; } = "R";
        public int[,] FlippedCards { get; set; }
        public string WinnerColor { get; set; }

        public string RoomCode { get; set; }

        public PairMatching()
        {
            GenerateBoard();
            FlippedCards = new int[2, 2];
        }

        public string GetPlayerColor(string playerId)
        {
            return playerColors.ContainsKey(playerId) ? playerColors[playerId] : null;
        }

        public void AssignPlayerColors(string player1Id, string player2Id)
        {
            playerColors[player1Id] = "R";
            playerColors[player2Id] = "Y";
        }
        public override Task HandleCommand(string playerId, string command, IHubCallerClients clients)
        {
            if (command.StartsWith("getBoard"))
            {
                return clients.Caller.SendAsync("ReceiveBoard", GetGameState());
            }
            if (command.StartsWith("flip"))
            {
                string[] parts = command.Split(' ');
                Card card = Board[int.Parse(parts[1]), int.Parse(parts[2])];
                if (card.state == CardState.FaceDown)
                {
                    
                }

            }

            return Task.CompletedTask;
        }

        private void GenerateBoard()
        {
            var values = new List<int>();
            for (int i = 1; i <= 9; i++)
            {
                values.Add(i);
                values.Add(i);
            }
            var rand = new Random();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int index = rand.Next(values.Count);
                    Board[i, j] = new Card(values[index]);
                    values.RemoveAt(index);
                }
            }
        }
        public Object GetGameState()
        {
            var boardState = new List<string[]>();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var card = Board[i, j];
                    boardState.Add(new string[] { card.Value.ToString(), card.state.ToString() }); // Corrected string array creation
                }
            }
            return new
            {
                BoardState = boardState,
                CurrentPlayerColor,
                FlippedCards,
                WinnerColor
            };
        }
    }
}