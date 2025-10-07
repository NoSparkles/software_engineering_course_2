using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class User
    {
        [Key]
        public string Username { get; set; } = null!;
        public string PlayerId => Username;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public List<string> Friends { get; set; } = new List<string>();
        public List<string> IncomingFriendRequests { get; set; } = new List<string>();
        public List<string> OutgoingFriendRequests { get; set; } = new List<string>();

        public int RockPaperScissorsMMR { get; set; } = 1000;
        public int FourInARowMMR { get; set; } = 1000;
        public int PairMatchingMMR { get; set; } = 1000;

        public int RockPaperScissorsWinStreak { get; set; } = 0;
        public int FourInARowWinStreak { get; set; } = 0;
        public int PairMatchingWinStreak { get; set; } = 0;
    }
}
 