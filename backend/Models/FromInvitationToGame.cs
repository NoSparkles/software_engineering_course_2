namespace Models
{
    // Owned entity: no [Key] or navigation properties needed
    public class FromInvitationToGame : IEquatable<FromInvitationToGame>
    {
        public string RoomKey { get; set; } = null!;
        public string FromUsername { get; set; } = null!;

        public FromInvitationToGame() { }

        public FromInvitationToGame(string fromUsername, string roomKey)
        {
            FromUsername = fromUsername ?? throw new ArgumentNullException(nameof(fromUsername));
            RoomKey = roomKey ?? throw new ArgumentNullException(nameof(roomKey));
        }

        public bool Equals(FromInvitationToGame? other) =>
            other is not null &&
            FromUsername == other.FromUsername &&
            RoomKey == other.RoomKey;

        public override bool Equals(object? obj) => Equals(obj as FromInvitationToGame);
        public override int GetHashCode() => HashCode.Combine(FromUsername, RoomKey);
    }
}
