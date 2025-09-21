using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;

namespace Services
{
    public class UserService
    {
        private readonly GameDbContext _context;

        public UserService(GameDbContext context)
        {
            _context = context;
        }

        // Get a user by username
        public async Task<User?> GetUserAsync(string username)
        {
            return await _context.Users.FindAsync(username);
        }

        // Register a new user with username and password only
        public async Task<bool> RegisterUserAsync(string username, string password)
        {
            if (await _context.Users.AnyAsync(u => u.Username == username))
                return false;

            var hashedPassword = HashPassword(password);

            var newUser = new User
            {
                Username = username,
                PasswordHash = hashedPassword,
                Friends = new List<string>(),
                RockPaperScissorsMMR = 1000,
                FourInARowMMR = 1000,
                PairMatchingMMR = 1000,
                TournamentMMR = 1000
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return true;
        }

        // Add a friend (mutual)
        public async Task<bool> AddFriendAsync(string username, string friendUsername)
        {
            var user = await _context.Users.FindAsync(username);
            var friend = await _context.Users.FindAsync(friendUsername);

            if (user == null || friend == null)
                return false;

            if (!user.Friends.Contains(friendUsername))
                user.Friends.Add(friendUsername);

            if (!friend.Friends.Contains(username))
                friend.Friends.Add(username);

            await _context.SaveChangesAsync();
            return true;
        }

        // Update MMR
        public async Task<bool> UpdateMMRAsync(string username, Dictionary<string, int> mmrUpdates)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null)
                return false;

            foreach (var kvp in mmrUpdates)
            {
                switch (kvp.Key.ToLower())
                {
                    case "rockpaperscissors":
                        user.RockPaperScissorsMMR = kvp.Value;
                        break;
                    case "fourinarow":
                        user.FourInARowMMR = kvp.Value;
                        break;
                    case "pairmatching":
                        user.PairMatchingMMR = kvp.Value;
                        break;
                    case "tournament":
                        user.TournamentMMR = kvp.Value;
                        break;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // List all users
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // Simple SHA256 password hash
        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
