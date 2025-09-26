using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace games
{
    public class FourInARowGame : GameInstance
    {
        private Dictionary<string, string> playerColors = new();
        public string[,] Board { get; private set; } = new string[6, 7];
       public string CurrentPlayerColor { get; private set; } = "R";
        // WinnerColor is null when there is no winner yet
        public string? WinnerColor { get; private set; } = null;
        // RoomCode will be set by the hub when the game is created
        public bool IsValidMove(int col)
        {
            return col >= 0 && col < 7 && Board[0, col] == null;
        }

        public bool ApplyMove(int col, string playerColor)
        {
            if (WinnerColor != null || playerColor != CurrentPlayerColor || !IsValidMove(col))
                return false;

            for (int row = 5; row >= 0; row--)
            {
                if (Board[row, col] == null)
                {
                    Board[row, col] = playerColor;
                    if (CheckWin(row, col, playerColor))
                        WinnerColor = playerColor;
                    else
                        CurrentPlayerColor = (CurrentPlayerColor == "R") ? "Y" : "R";
                    return true;
                }
            }
            return false;
        }

        private bool CheckWin(int row, int col, string color)
        {
            int[][] directions = new int[][] {
                new int[]{0,1}, new int[]{1,0}, new int[]{1,1}, new int[]{1,-1}
            };
            foreach (var dir in directions)
            {
                int count = 1;
                count += CountDirection(row, col, dir[0], dir[1], color);
                count += CountDirection(row, col, -dir[0], -dir[1], color);
                if (count >= 4) return true;
            }
            return false;
        }

        private int CountDirection(int row, int col, int dRow, int dCol, string color)
        {
            int count = 0;
            for (int i = 1; i < 4; i++)
            {
                int r = row + dRow * i;
                int c = col + dCol * i;
                if (r < 0 || r >= 6 || c < 0 || c >= 7 || Board[r, c] != color)
                    break;
                count++;
            }
            return count;
        }

        public override async Task HandleCommand(string playerId, string command, IHubCallerClients clients, RoomUser user)
        {
            if (command.StartsWith("MOVE:"))
            { 
                var color = GetPlayerColor(user);
                if (color == null) return;
                if (!int.TryParse(command.Substring(5), out int col)) return;
                if (ApplyMove(col, color))
                {
                    // Convert Board to jagged array for JS
                    var boardToSend = new string[6][];
                    for (int r = 0; r < 6; r++)
                    {
                        boardToSend[r] = new string[7];
                        for (int c = 0; c < 7; c++)
                            boardToSend[r][c] = Board[r, c];
                    }
                    await clients.Group(RoomCode).SendAsync("ReceiveMove", new
                    {
                        board = boardToSend,
                        currentPlayer = CurrentPlayerColor,
                        winner = WinnerColor
                    });
                }
            }

            if (command.StartsWith("RESET"))
            {
                Board = new string[6, 7];
                CurrentPlayerColor = "R";
                WinnerColor = null;
                await clients.Group(RoomCode).SendAsync("GameReset");
            }
        }

        // Provide current game state for spectators
        public object GetGameState()
        {
            var boardToSend = new string[6][];
            for (int r = 0; r < 6; r++)
            {
                boardToSend[r] = new string[7];
                for (int c = 0; c < 7; c++)
                    boardToSend[r][c] = Board[r, c];
            }

            return new
            {
                board = boardToSend,
                currentPlayer = CurrentPlayerColor,
                winner = WinnerColor
            };
        }
    }
}