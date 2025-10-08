namespace Models.InMemoryModels
{
    public class RoomUser
    {
        public string? PlayerId { get; set; }
        public string? Username { get; set; }
        public User? User { get; set; }
        public bool IsPlayer { get; set; }

        public RoomUser() { }

        public RoomUser(string playerId, bool isPlayer, User user)
        {
            PlayerId = playerId;
            IsPlayer = isPlayer;
            User = user;
            Username = user?.Username;
        }

        public RoomUser(string playerId, User user)
        {
            PlayerId = playerId;
            User = user;
            Username = user?.Username;
            IsPlayer = true;
        }
    }
}