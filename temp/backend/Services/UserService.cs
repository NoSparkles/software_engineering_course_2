using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.VisualBasic;

namespace Services
{
    public class UserService
    {
        static public byte[] KEY = Encoding.ASCII.GetBytes("hc328fh283h23d89h32d3g2hd7820hd8237h238d7h27f832hf2o783hfo782g7832fg7o28gf7238o");
        private JwtSecurityTokenHandler TokenHandler = new JwtSecurityTokenHandler();
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

        public string GenerateJwtToken(User user)
        {
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Username)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(KEY), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = TokenHandler.CreateToken(tokenDescriptor);
            return TokenHandler.WriteToken(token);
        }

        public async Task<User?> GetUserFromTokenAsync(string jwtToken)
        {
            try
            {
                var principal = TokenHandler.ValidateToken(jwtToken, new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(KEY),
                    ClockSkew = TimeSpan.Zero
                }, out _);

                var username = principal.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrWhiteSpace(username))
                    return null;

                return await GetUserAsync(username);
            }
            catch
            {
                return null;
            }
        }


        public async Task<bool> DeleteUserAsync(string username)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null)
                return false;

            _context.Users.Remove(user);
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
        public string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
