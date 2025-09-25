using System.Security.Permissions;
using games;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Models.InMemoryModels
{
    public class Room
    {
        private GameInstance Game { get; set; }
        private List<RoomUser> RoomUsers { get; set; }
        private string Code { get; set; }

        public Room(GameInstance game, string roomCode)
        {
            Game = game;
            RoomUsers = new List<RoomUser>();
            Code = roomCode;
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
    }
}