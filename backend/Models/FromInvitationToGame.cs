using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class FromInvitationToGame : IEquatable<FromInvitationToGame>
    {
        [Key]
        public string RoomKey { get; set; }
        public string ToUsername { get; set; }

        public FromInvitationToGame(string toUsername, string roomKey)
        {
            ToUsername = toUsername ?? throw new ArgumentNullException(nameof(toUsername));
            RoomKey = roomKey ?? throw new ArgumentNullException(nameof(roomKey));
        }

        public bool Equals(FromInvitationToGame? other) =>
            other is not null &&
            ToUsername == other.ToUsername &&
            RoomKey == other.RoomKey;

        public override bool Equals(object? obj) => Equals(obj as FromInvitationToGame);
        public override int GetHashCode() => HashCode.Combine(ToUsername, RoomKey);
    }
}