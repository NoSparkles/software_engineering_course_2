using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Models
{
    public class User
    {
        [Key]
        public string Username { get; set; } = null!; // Primary key

        // Hashed password
        [Required]
        public string PasswordHash { get; set; } = null!;

        // Friends list (usernames)
        public List<string> Friends { get; set; } = new List<string>();

        // Matchmaking / Ranking points for each game
        public int RockPaperScissorsMMR { get; set; } = 1000;
        public int FourInARowMMR { get; set; } = 1000;
        public int PairMatchingMMR { get; set; } = 1000;
        public int TournamentMMR { get; set; } = 1000;

        // Win streaks
        public int RockPaperScissorsWinStreak { get; set; } = 0;
        public int FourInARowWinStreak { get; set; } = 0;
        public int PairMatchingWinStreak { get; set; } = 0;
        public int TournamentWinStreak { get; set; } = 0;
    }
}
