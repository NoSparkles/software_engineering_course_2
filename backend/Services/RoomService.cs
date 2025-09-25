using System.Runtime.CompilerServices;
using Models.InMemoryModels;

namespace Services
{
    public class RoomService
    {
        private Dictionary<string, Room> Rooms { get; set; } // roomKey -> Room
        private Dictionary<string, RoomUser> CodeRoomUsers { get; set; } // playerId -> RoomUser

        private Dictionary<string, RoomUser> MatchMakingRoomUsers { get; set; }

        RoomService()
        {
            Rooms = new Dictionary<string, Room>();
            CodeRoomUsers = new Dictionary<string, RoomUser>();
            MatchMakingRoomUsers = new Dictionary<string, RoomUser>();
        }

        public Task<string> CreateRoom(string gameType)
        {
            var roomCode = GenerateRoomCode();
            var roomKey = CreateRoomKey(gameType, roomCode);
            var game = Room.GameTypeToGame(gameType);
            var newRoom = new Room(game, roomCode);
            Rooms[roomKey] = newRoom;
            return Task.FromResult(roomCode);
        }

        private string GenerateRoomCode(int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rng = new Random();
            string code;

            do
            {
                code = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[rng.Next(s.Length)]).ToArray());
            }
            while (Rooms.ContainsKey(CreateRoomKey("four-in-a-row", code)) ||
                   Rooms.ContainsKey(CreateRoomKey("pair-matching", code)) ||
                   Rooms.ContainsKey(CreateRoomKey("rock-paper-scissors", code)));

            return code;
        }

        public Task<bool> RoomExists(string gameType, string roomCode)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            return Task.FromResult(Rooms.ContainsKey(roomKey));
        }

        public string CreateRoomKey(string gameType, string roomCode)
        {
            return $"{gameType}:{roomCode.ToUpper()}";
        }
    }
}