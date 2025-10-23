using System.Collections.Generic;
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

        public List<string> Friends { get; set; } = new List<string>();
        public List<string> IncomingFriendRequests { get; set; } = new List<string>();
        public List<string> OutgoingFriendRequests { get; set; } = new List<string>();
        public HashSet<ToInvitationToGame> OutcomingInviteToGameRequests { get; set; } = new HashSet<ToInvitationToGame>();
        public HashSet<FromInvitationToGame> IncomingInviteToGameRequests { get; set; } = new HashSet<FromInvitationToGame>();

        public int RockPaperScissorsMMR { get; set; } = 1000;
        public int FourInARowMMR { get; set; } = 1000;
        public int PairMatchingMMR { get; set; } = 1000;

        public int RockPaperScissorsWinStreak { get; set; } = 0;
        public int FourInARowWinStreak { get; set; } = 0;
        public int PairMatchingWinStreak { get; set; } = 0;

        public bool Equals(User? other)
        {
            if (other == null) return false;
            return Username == other.Username;
        }

       
        public override bool Equals(object? obj)
        {
            return Equals(obj as User);
        }

        public override int GetHashCode()
        {
            return Username.GetHashCode();
        }

      
        public static bool operator ==(User? left, User? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(User? left, User? right)
        {
            return !(left == right);
        }
    }
}