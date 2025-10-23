using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class InvitationToGame : IEquatable<InvitationToGame>
    {
        public string FromUsername { get; set; }

        [Key]
        public string RoomKey { get; set; }

        public InvitationToGame(string fromUsername, string roomKey)
        {
            FromUsername = fromUsername ?? throw new ArgumentNullException(nameof(fromUsername));
            RoomKey = roomKey ?? throw new ArgumentNullException(nameof(roomKey));
        }

        public bool Equals(InvitationToGame? other)
        {
            return other is not null &&
                   FromUsername == other.FromUsername &&
                   RoomKey == other.RoomKey;
        }

        public override bool Equals(object? obj) => Equals(obj as InvitationToGame);

        public override int GetHashCode() => HashCode.Combine(FromUsername, RoomKey);
    }
}