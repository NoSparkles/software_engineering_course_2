using Microsoft.AspNetCore.SignalR;
using Models.InMemoryModels;

namespace Games
{
    public class PairMatching : GameInstance
    {
        private Dictionary<string, int> Scores = new();
        private Card[,] Board { get; set; } = new Card[3, 6];
        public string CurrentPlayerColor { get; private set; } = "R";
        public List<List<int>> FlippedCards { get; set; }
    // WinnerColor may be empty when there is no winner yet
        public string WinnerColor { get; set; } = "";
        private Dictionary<string, bool> resetVotes = new();


        public PairMatching()
        {
            GenerateBoard();
            FlippedCards = new List<List<int>>();
            Scores["R"] = 0;
            Scores["Y"] = 0;
            resetVotes["R"] = false;
            resetVotes["Y"] = false;
        }

        public override async Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user)
        {
            if (FlippedCards.Count == 2)
            {
                Board[FlippedCards[0][0], FlippedCards[0][1]].state = CardState.FaceDown;
                Board[FlippedCards[1][0], FlippedCards[1][1]].state = CardState.FaceDown;
                FlippedCards.Clear();
            }
            
            if (command.StartsWith("getBoard"))
            {
                await clients.Caller.SendAsync("ReceiveBoard", GetGameState());
            }
            else if (command.StartsWith("flip"))
            {
                // Prevent spectators / unknown ids from performing flips
                var color = GetPlayerColor(user);
                if (color == null) return;

                string[] parts = command.Split(' ');
                var col = int.Parse(parts[1]);
                var row = int.Parse(parts[2]);
                if (Board[row, col].state == CardState.FaceDown)
                {
                    Board[row, col].state = CardState.FaceUp;
                    FlippedCards.Add(new List<int> { row, col });
                }

                if (FlippedCards.Count == 2)
                {
                    if (Board[FlippedCards[0][0], FlippedCards[0][1]].Value == Board[FlippedCards[1][0], FlippedCards[1][1]].Value)
                    {
                        Board[FlippedCards[0][0], FlippedCards[0][1]].state = CardState.Matched;
                        Board[FlippedCards[1][0], FlippedCards[1][1]].state = CardState.Matched;
                        FlippedCards.Clear();
                        Scores[CurrentPlayerColor] += 1;
                        if (Scores[CurrentPlayerColor] == 5)
                        {
                            WinnerColor = CurrentPlayerColor;
                            await clients.Group(RoomCode).SendAsync("GameOver", WinnerColor);
                        }
                        else if (IsGameComplete())
                        {
                            // Check if all pairs are matched but no player reached 5 points
                            WinnerColor = Scores["R"] == Scores["Y"] ? "DRAW" : (Scores["R"] > Scores["Y"] ? "R" : "Y");
                            await clients.Group(RoomCode).SendAsync("GameOver", WinnerColor);
                        }
                    }
                    CurrentPlayerColor = CurrentPlayerColor == "R" ? "Y" : "R";
                }

                await clients.Group(RoomCode).SendAsync("ReceiveBoard", GetGameState());
            }
            else if (command.StartsWith("reset"))
            {
                var color = GetPlayerColor(user);
                if (color == null) return;

                resetVotes[color] = true;
                if (resetVotes["R"] && resetVotes["Y"])
                {
                    ResetGame();
                    await clients.Group(RoomCode).SendAsync("ResetGame", GetGameState());
                }
            }
        }

        private bool IsGameComplete()
        {
            // Check if all cards are matched
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if (Board[i, j].state != CardState.Matched)
                        return false;
                }
            }
            return true;
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

        public Card[,] GetBoard()
        {
            return Board;
        }

        public Dictionary<string, int> GetScores()
        {
            return Scores;
        }

        private void ResetGame()
        {
            GenerateBoard();
            FlippedCards.Clear();
            WinnerColor = "";
            CurrentPlayerColor = "R";
            Scores["R"] = 0;
            Scores["Y"] = 0;
            resetVotes["R"] = false;
            resetVotes["Y"] = false;
        }
        public GameState GetGameState()
        {
            var boardState = new List<CardInfo>();

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    var card = Board[i, j];
                    boardState.Add(new CardInfo
                    {
                        Value = card.Value,
                        State = card.state.ToString(),
                        X = i,
                        Y = j
                    });
                }
            }

            var flippedIndices = FlippedCards
                .Select(coords => coords[0] * 6 + coords[1])
                .ToList();

            return new GameState
            {
                Board = boardState,
                CurrentPlayer = CurrentPlayerColor,
                Flipped = flippedIndices,
                Scores = Scores,
                Winner = WinnerColor ?? ""
            };
        }


        public override async Task ReportWin(string playerId, IHubCallerClients clients)
        {
            Console.WriteLine($"PairMatching: ReportWin called for player {playerId}, winner is {WinnerColor}");
            // Win already reported through GameOver event in HandleCommand
            await Task.CompletedTask;
        }
    }
}