using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;
using System.Linq;

namespace games
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

        public override Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user)
        {
            if (FlippedCards.Count() == 2)
            {
                Board[FlippedCards[0][0], FlippedCards[0][1]].state = CardState.FaceDown;
                Board[FlippedCards[1][0], FlippedCards[1][1]].state = CardState.FaceDown;
                FlippedCards.Clear();

            }
            if (command.StartsWith("getBoard"))
            {
                clients.Caller.SendAsync("ReceiveBoard", GetGameState());
            }
            else if (command.StartsWith("flip"))
            {
                // Prevent spectators / unknown ids from performing flips
                var color = GetPlayerColor(user);
                if (color == null) return Task.CompletedTask;

                string[] parts = command.Split(' ');
                var col = int.Parse(parts[1]);
                var row = int.Parse(parts[2]);
                if (Board[row, col].state == CardState.FaceDown)
                {
                    Board[row, col].state = CardState.FaceUp;
                    FlippedCards.Add(new List<int> { row, col });
                }

                if (FlippedCards.Count() == 2)
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
                        }
                    }
                    CurrentPlayerColor = CurrentPlayerColor == "R" ? "Y" : "R";
                }

                clients.Group(RoomCode).SendAsync("ReceiveBoard", GetGameState());
            }
            else if (command.StartsWith("reset"))
            {
                var color = GetPlayerColor(user);
                if (color == null) return Task.CompletedTask;

                resetVotes[color] = true;
                if (resetVotes["R"] && resetVotes["Y"])
                {
                    ResetGame();
                    clients.Group(RoomCode).SendAsync("ResetGame", GetGameState());
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
                scores = Scores,
                winner = WinnerColor ?? "",

            };
        }
    }
}