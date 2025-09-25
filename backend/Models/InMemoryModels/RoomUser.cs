using System.Runtime.CompilerServices;

namespace Models.InMemoryModels
{
    public record class RoomUser
    {
        public string PlayerId { get; set; }
        public bool IsPlayer { get; set; }
        public User? User { get; set; }
        public string ConnectionId { get; set; }

        public RoomUser(string playerId, bool isPlayer, User? user, string connectionId)
        {
            PlayerId = playerId;
            IsPlayer = isPlayer;
            User = user;
            ConnectionId = connectionId;
        }
    }
}