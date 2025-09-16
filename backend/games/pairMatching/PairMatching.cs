using Microsoft.AspNetCore.SignalR;

namespace games
{
    public class PairMatching : GameInstance
    {
        public Dictionary<string, string> playerColors = new();
        private Card[,] Board { get; set; } = new Card[3, 6];
        public string CurrentPlayerColor { get; private set; } = "R";
        public List<List<int>> FlippedCards { get; set; }
        public string WinnerColor { get; set; }

        public string RoomCode { get; set; }

        public PairMatching()
        {
            GenerateBoard();
            FlippedCards = new List<List<int>>();
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
            if (FlippedCards.Count() == 2)
            {
                Card first = Board[FlippedCards[0][0], FlippedCards[0][1]];
                Card second = Board[FlippedCards[1][0], FlippedCards[1][1]];
                // they are not matching for sure
                first.state = CardState.FaceDown;
                second.state = CardState.FaceDown;
                FlippedCards.Clear();

            }
            if (command.StartsWith("getBoard"))
            {
                return clients.Caller.SendAsync("ReceiveBoard", GetGameState());
            }
            else if (command.StartsWith("flip"))
            {
                string[] parts = command.Split(' ');
                var col = int.Parse(parts[1]);
                var row = int.Parse(parts[2]);
                Card card = Board[row, col];
                Console.WriteLine("card: {0} {1} {2}", int.Parse(parts[1]), int.Parse(parts[2]), playerColors[playerId]);
                if (card.state == CardState.FaceDown)
                {
                    card.state = CardState.FaceUp;
                    FlippedCards.Add(new List<int> { row, col });
                }

                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        Console.Write("{0} ", Board[i, j].state == CardState.FaceDown ? -1 : Board[i, j].Value);
                    }
                    Console.WriteLine();
                }

                if (FlippedCards.Count() == 2)
                {
                    Card first = Board[FlippedCards[0][0], FlippedCards[0][1]];
                    Card second = Board[FlippedCards[1][0], FlippedCards[1][1]];

                    if (first.Value == second.Value)
                    {
                        first.state = CardState.Matched;
                        second.state = CardState.Matched;
                        FlippedCards.Clear();
                    }
                    CurrentPlayerColor = CurrentPlayerColor == "R" ? "Y" : "R";
                }
                return clients.Group(RoomCode).SendAsync("ReceiveBoard", GetGameState());
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
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    int index = rand.Next(values.Count);
                    Board[i, j] = new Card(values[index]);
                    values.RemoveAt(index);
                }
            }
        }
        public object GetGameState()
        {
            var boardState = new List<object>();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    var card = Board[i, j];
                    boardState.Add(new
                    {
                        value = card.Value,
                        state = card.state.ToString(),
                        x = i,
                        y = j
                    });
                }
            }

            // Convert flipped (row, col) pairs to flat indices
            var flippedIndices = FlippedCards
                .Select(coords => coords[0] * 6 + coords[1])
                .ToList();

            return new
            {
                board = boardState,
                currentPlayer = CurrentPlayerColor,
                flipped = flippedIndices,
                winner = WinnerColor ?? ""
            };
        }
    }
}