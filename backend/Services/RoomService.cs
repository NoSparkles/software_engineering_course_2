using games;
using Microsoft.AspNetCore.SignalR;
using Models;
using Models.InMemoryModels;

namespace Services
{
    public class RoomService
    {
        public Dictionary<string, Room> Rooms { get; set; } // roomKey -> Room
        public Dictionary<string, RoomUser> CodeRoomUsers { get; set; } // playerId -> RoomUser
        public Dictionary<string, RoomUser> MatchMakingRoomUsers { get; set; } // 

        public RoomService()
        {
            Rooms = new Dictionary<string, Room>();
            CodeRoomUsers = new Dictionary<string, RoomUser>();
            MatchMakingRoomUsers = new Dictionary<string, RoomUser>();
        }

        public string CreateRoom(string gameType, bool isMatchMaking)
        {
            var roomCode = GenerateRoomCode();
            var roomKey = CreateRoomKey(gameType, roomCode);
            var game = Room.GameTypeToGame(gameType);
            var newRoom = new Room(game, isMatchMaking, roomCode);
            Rooms[roomKey] = newRoom;
            return roomCode;
        }

        public Room GetRoomByKey(string roomKey)
        {
            return Rooms[roomKey];
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

        public bool RoomExists(string gameType, string roomCode)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            return Rooms.ContainsKey(roomKey);
        }

        public string CreateRoomKey(string gameType, string roomCode)
        {
            return $"{gameType}:{roomCode.ToUpper()}";
        }

        public async Task JoinAsPlayerNotMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId, IHubCallerClients clients)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var room = GetRoomByKey(roomKey);
            var game = room.Game;
            var roomPlayers = room.RoomPlayers;
            var roomUser = roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);

            if (roomUser is null)
            {
                roomPlayers.Add(new RoomUser(playerId, true, user, connectionId));
            }
            else
            {
                roomUser.ConnectionId = connectionId;
            }


            bool shouldNotifyStart;
            lock (roomPlayers)
            {
                shouldNotifyStart = roomPlayers.Count == 2;
            }

            if (shouldNotifyStart)
            {
                game.RoomCode = roomKey;
                game.AssignPlayerColors(roomPlayers[0], roomPlayers[1]);
                foreach (var rp in roomPlayers)
                    await clients.Client(rp.ConnectionId).SendAsync("SetPlayerColor", game.GetPlayerColor(rp) ?? "");

                await clients.Group(roomKey).SendAsync("StartGame", roomCode);
            }
            else
            {
                switch (game)
                {
                    case FourInARowGame fourGame:
                        await clients.Caller.SendAsync("ReceiveMove", fourGame.GetGameState());
                        break;

                    case PairMatching pairGame:
                        await clients.Caller.SendAsync("ReceiveBoard", pairGame.GetGameState());
                        break;

                    case RockPaperScissors rpsGame:
                        await clients.Caller.SendAsync("ReceiveRpsState", rpsGame.GetGameStatePublic());
                        break;
                }
            }
        }

        public RoomUser? GetRoomUser(string roomKey, string playerId, User? user) {
            var roomPlayers = GetRoomByKey(roomKey).RoomPlayers;
            return roomPlayers.Find(rp => (rp.User is not null && rp.User == user) || rp.PlayerId == playerId);
        }

        public async Task JoinAsPlayerMatchMaking(string gameType, string roomCode, string playerId, User? user, string connectionId)
        {
            // TODO
        }

        public void JoinAsSpectator(string gameType, string roomCode, string playerId, User? user, string connectionId)
        {
            var roomKey = CreateRoomKey(gameType, roomCode);
            var RoomUser = new RoomUser(playerId, false, user, connectionId);
            // TODO
        }
    }
}