using Microsoft.AspNetCore.SignalR;

namespace games
{
    public class PairMatching : GameInstance
    {
        public Dictionary<string, string> playerColors = new();
        private Card[,] Board { get; set; } = new Card[6, 3];
        public string CurrentPlayerColor { get; private set; } = "R";
        public string WinnerColor { get; set; }

        public string RoomCode { get; set; }

        private class Card
        {
            public int Value { get; set; }
            public CardState state { get; set; }

            public Card(int value)
            {
                Value = value;
                state = CardState.FaceDown;
            }
        }

        private enum CardState
        {
            FaceDown,
            FaceUp,
            Matched
        }

        public PairMatching()
        {
            GenerateBoard();
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
                Console.WriteLine("received get board");
                return clients.Caller.SendAsync("ReceiveBoard", GetBoardState());
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
        public List<string[]> GetBoardState()
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
            return boardState;
        }
    }
}