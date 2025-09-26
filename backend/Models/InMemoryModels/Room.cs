using System.Security.Permissions;
using games;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Models.InMemoryModels
{
    public class Room
    {
        public GameInstance Game { get; set; }
        public bool IsMatchMaking { get; set; }
        public List<RoomUser> RoomPlayers { get; set; }
        public List<RoomUser> RoomSpectators { get; set; }
        public string Code { get; set; }

        public bool GameStarted { get; set; }

        public Room(GameInstance game, bool isMatchMaking, string roomCode)
        {
            Game = game;
            IsMatchMaking = isMatchMaking;
            RoomPlayers = new List<RoomUser>();
            RoomSpectators = new List<RoomUser>();
            Code = roomCode;
            GameStarted = false;
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