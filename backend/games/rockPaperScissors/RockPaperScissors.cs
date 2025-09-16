using Microsoft.AspNetCore.SignalR;
using System.Text.Json.Serialization;

namespace games
{
    public class RockPaperScissors : GameInstance
    {
        private readonly Dictionary<string, string> playerColors = new();
        public string RoomCode { get; set; } = "";

        private const int MaxRounds = 5;
        private const int WinsToFinish = 3;

        private readonly Dictionary<string, int> Scores = new() { ["R"] = 0, ["Y"] = 0 };
        private int RoundsPlayed = 0;
        public string WinnerColor { get; private set; } = "";

        private readonly Dictionary<string, string?> CurrentChoices = new() { ["R"] = null, ["Y"] = null };
        private readonly List<RoundResult> History = new();
        private readonly Dictionary<string, bool> resetVotes = new() { ["R"] = false, ["Y"] = false };
        private bool LastRoundWasDraw = false;

        private class RoundResult
        {
            [JsonPropertyName("round")] public int Round { get; set; }
            [JsonPropertyName("R")] public string RChoice { get; set; } = "";
            [JsonPropertyName("Y")] public string YChoice { get; set; } = "";
            [JsonPropertyName("winner")] public string Winner { get; set; } = "";
        }

        public void AssignPlayerColors(string player1Id, string player2Id)
        {
            playerColors[player1Id] = "R";
            playerColors[player2Id] = "Y";
        }

        public string? GetPlayerColor(string playerId)
        {
            return playerColors.TryGetValue(playerId, out var color) ? color : null;
        }

        public override async Task HandleCommand(string playerId, string command, IHubCallerClients clients)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            if (command.StartsWith("getState", StringComparison.OrdinalIgnoreCase))
            {
                await clients.Caller.SendAsync("ReceiveRpsState", GetGameState());
                return;
            }

            if (command.StartsWith("CHOOSE:", StringComparison.OrdinalIgnoreCase))
            {
                var color = GetPlayerColor(playerId);
                if (color == null || IsMatchOver()) return;

                var choice = command.Substring("CHOOSE:".Length).Trim().ToLowerInvariant();
                if (!IsValidChoice(choice)) return;

                if (CurrentChoices[color] == null) CurrentChoices[color] = choice;

                if (CurrentChoices["R"] != null && CurrentChoices["Y"] != null)
                    await ResolveRoundAndBroadcast(clients);
                else
                    await clients.Group(RoomCode).SendAsync("ReceiveRpsState", GetGameState());

                return;
            }

            if (command.StartsWith("RESET", StringComparison.OrdinalIgnoreCase))
            {
                var color = GetPlayerColor(playerId);
                if (color == null) return;

                resetVotes[color] = true;
                if (resetVotes["R"] && resetVotes["Y"])
                {
                    ResetMatch();
                    await clients.Group(RoomCode).SendAsync("RpsReset", GetGameState());
                }
                else
                {
                    await clients.Group(RoomCode).SendAsync("ReceiveRpsState", GetGameState());
                }

                return;
            }
        }

        private static bool IsValidChoice(string c) =>
            c == "rock" || c == "paper" || c == "scissors";

        private async Task ResolveRoundAndBroadcast(IHubCallerClients clients)
        {
            var r = CurrentChoices["R"]!;
            var y = CurrentChoices["Y"]!;

            if (r == y)
            {
                LastRoundWasDraw = true;
                CurrentChoices["R"] = null;
                CurrentChoices["Y"] = null;
                await clients.Group(RoomCode).SendAsync("ReceiveRpsState", GetGameState());
                return;
            }

            LastRoundWasDraw = false;

            var winner = DecideWinner(r, y);
            if (winner == "R") Scores["R"]++; else Scores["Y"]++;

            RoundsPlayed++;
            History.Add(new RoundResult { Round = RoundsPlayed, RChoice = r, YChoice = y, Winner = winner });

            CurrentChoices["R"] = null;
            CurrentChoices["Y"] = null;

            if (Scores["R"] >= WinsToFinish) WinnerColor = "R";
            else if (Scores["Y"] >= WinsToFinish) WinnerColor = "Y";
            else if (RoundsPlayed >= MaxRounds)
            {
                WinnerColor = Scores["R"] == Scores["Y"] ? "DRAW" : (Scores["R"] > Scores["Y"] ? "R" : "Y");
            }

            await clients.Group(RoomCode).SendAsync("ReceiveRpsState", GetGameState());
        }

        private static string DecideWinner(string rChoice, string yChoice)
        {
            bool rBeatsY =
                (rChoice == "rock" && yChoice == "scissors") ||
                (rChoice == "paper" && yChoice == "rock") ||
                (rChoice == "scissors" && yChoice == "paper");
            return rBeatsY ? "R" : "Y";
        }

        private bool IsMatchOver() => !string.IsNullOrEmpty(WinnerColor);

        private void ResetMatch()
        {
            Scores["R"] = 0;
            Scores["Y"] = 0;
            RoundsPlayed = 0;
            WinnerColor = "";
            History.Clear();
            CurrentChoices["R"] = null;
            CurrentChoices["Y"] = null;
            resetVotes["R"] = false;
            resetVotes["Y"] = false;
            LastRoundWasDraw = false;
        }

        private object GetGameState()
        {
            return new
            {
                maxRounds = MaxRounds,
                winsToFinish = WinsToFinish,
                roundsPlayed = RoundsPlayed,
                scores = new { R = Scores["R"], Y = Scores["Y"] },
                currentChoices = new { R = CurrentChoices["R"], Y = CurrentChoices["Y"] },
                history = History,
                winner = WinnerColor,
                lastDraw = LastRoundWasDraw
            };
        }
    }
}
