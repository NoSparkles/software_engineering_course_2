using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class User : IEquatable<User>
    {
        [Key]
        public string Username { get; set; } = null!;
        public string PlayerId => Username;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public List<string> Friends { get; set; } = new();
        public List<string> IncomingFriendRequests { get; set; } = new();
        public List<string> OutgoingFriendRequests { get; set; } = new();

        // Owned collections
        public List<ToInvitationToGame> OutcomingInviteToGameRequests { get; set; } = new();
        public List<FromInvitationToGame> IncomingInviteToGameRequests { get; set; } = new();

        public int RockPaperScissorsMMR { get; set; } = 1000;
        public int FourInARowMMR { get; set; } = 1000;
        public int PairMatchingMMR { get; set; } = 1000;

        public int RockPaperScissorsWinStreak { get; set; } = 0;
        public int FourInARowWinStreak { get; set; } = 0;
        public int PairMatchingWinStreak { get; set; } = 0;

        public bool Equals(User? other) => other != null && Username == other.Username;

        public override bool Equals(object? obj) => Equals(obj as User);
        public override int GetHashCode() => Username.GetHashCode();

        public static bool operator ==(User? left, User? right) => Equals(left, right);
        public static bool operator !=(User? left, User? right) => !(left == right);
    }
}
