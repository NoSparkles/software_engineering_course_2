namespace Models
{
    // Owned entity: no [Key] or navigation properties needed
    public class ToInvitationToGame : IEquatable<ToInvitationToGame>
    {
        public string RoomKey { get; set; } = null!;
        public string ToUsername { get; set; } = null!;

        public ToInvitationToGame() { }

        public ToInvitationToGame(string toUsername, string roomKey)
        {
            ToUsername = toUsername ?? throw new ArgumentNullException(nameof(toUsername));
            RoomKey = roomKey ?? throw new ArgumentNullException(nameof(roomKey));
        }

        public bool Equals(ToInvitationToGame? other) =>
            other is not null &&
            ToUsername == other.ToUsername &&
            RoomKey == other.RoomKey;

        public override bool Equals(object? obj) => Equals(obj as ToInvitationToGame);
        public override int GetHashCode() => HashCode.Combine(ToUsername, RoomKey);
    }
}
