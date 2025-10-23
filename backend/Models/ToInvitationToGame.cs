using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class ToInvitationToGame : IEquatable<ToInvitationToGame>
    {
        [Key]
        public int Id { get; set; }
        public string RoomKey { get; set; }
        public string FromUsername { get; set; }

        public ToInvitationToGame(string fromUsername, string roomKey)
        {
            FromUsername = fromUsername ?? throw new ArgumentNullException(nameof(fromUsername));
            RoomKey = roomKey ?? throw new ArgumentNullException(nameof(roomKey));
        }

        public bool Equals(ToInvitationToGame? other) =>
            other is not null &&
            FromUsername == other.FromUsername &&
            RoomKey == other.RoomKey;

        public override bool Equals(object? obj) => Equals(obj as ToInvitationToGame);
        public override int GetHashCode() => HashCode.Combine(FromUsername, RoomKey);
    }
}