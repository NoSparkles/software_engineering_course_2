using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Extensions;
using System.Text.Json;
using Exceptions;

namespace Services
{
    public class UserService
    {
        static public readonly byte[] KEY;
        static UserService()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var path = Path.Combine(baseDir, "jwt.txt");

                if (!File.Exists(path))
                    throw new InvalidOperationException($"JWT key file not found. Place 'jwt.txt' in the app base directory. Expected path: {path}");

                using var fs = File.OpenRead(path);
                using var sr = new StreamReader(fs);
                var content = sr.ReadToEnd()?.Trim();
                if (string.IsNullOrEmpty(content))
                    throw new InvalidOperationException($"JWT key file is empty: {path}");

                KEY = Encoding.ASCII.GetBytes(content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load JWT key file: " + ex.Message, ex);
            }
        }
        private static readonly JwtSecurityTokenHandler TokenHandler = new JwtSecurityTokenHandler();
        private readonly GameDbContext _context;

        public UserService(GameDbContext context)
        {
            _context = context;
        }

        // Get a user by username
        public async Task<User> GetUserAsync(string username)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null)
                throw new UserNotFoundException(username, "USR_404", "GetUserAsync");
            return user;
        }

        public async Task<List<User>> SearchUsersAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<User>();

            // Case-insensitive search
            return await _context.Users
                .Where(u => u.Username.ToLower().Contains(query.ToLower()))
                .ToListAsync();
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

       // Send a friend request or accept if target already requested
        public async Task<bool> SendFriendRequestAsync(string username, string targetUsername)
        {
            if (username == targetUsername)
                return false; // can't friend yourself

            var user = await _context.Users.FindAsync(username);
            var target = await _context.Users.FindAsync(targetUsername);

            if (user == null || target == null)
                return false;

            // Already friends
            if (user.Friends.Contains(targetUsername))
                return false;

            // If target already sent a request to user => accept it
            if (user.IncomingFriendRequests.Contains(targetUsername))
            {
                user.IncomingFriendRequests.Remove(targetUsername);
                target.OutgoingFriendRequests.Remove(username);

                user.Friends.Add(targetUsername);
                target.Friends.Add(username);

                await _context.SaveChangesAsync();
                return true;
            }

            // Otherwise, send a request
            if (!user.OutgoingFriendRequests.Contains(targetUsername) &&
                !target.IncomingFriendRequests.Contains(username))
            {
                user.OutgoingFriendRequests.Add(targetUsername);
                target.IncomingFriendRequests.Add(username);

                await _context.SaveChangesAsync();
            }

            return true;
        }

        // Accept a friend request
        public async Task<bool> AcceptFriendRequestAsync(string username, string requesterUsername)
        {
            var user = await _context.Users.FindAsync(username);
            var requester = await _context.Users.FindAsync(requesterUsername);

            if (user == null || requester == null)
                return false;

            if (!user.IncomingFriendRequests.Contains(requesterUsername))
                return false; // no request to accept

            user.IncomingFriendRequests.Remove(requesterUsername);
            requester.OutgoingFriendRequests.Remove(username);

            user.Friends.Add(requesterUsername);
            requester.Friends.Add(username);

            await _context.SaveChangesAsync();
            return true;
        }

        // Reject a friend request
        public async Task<bool> RejectFriendRequestAsync(string username, string requesterUsername)
        {
            var user = await _context.Users.FindAsync(username);
            var requester = await _context.Users.FindAsync(requesterUsername);

            if (user == null || requester == null)
                return false;

            if (!user.IncomingFriendRequests.Contains(requesterUsername))
                return false;

            user.IncomingFriendRequests.Remove(requesterUsername);
            requester.OutgoingFriendRequests.Remove(username);

            await _context.SaveChangesAsync();
            return true;
        }

        // Remove a friend (mutual)
        public async Task<bool> RemoveFriendAsync(string username, string friendUsername)
        {
            var user = await _context.Users.FindAsync(username);
            var friend = await _context.Users.FindAsync(friendUsername);

            if (user == null || friend == null)
                return false;

            if (user.Friends.Contains(friendUsername))
                user.Friends.Remove(friendUsername);

            if (friend.Friends.Contains(username))
                friend.Friends.Remove(username);

            await _context.SaveChangesAsync();
            return true;
        }

        // fromUser in the next 3 methods is the user who sent the invitation
        public async Task<bool> InviteFriendToGame(string from, string to, string gameType, string code)
        {
            var fromUser = await _context.Users.FindAsync(from);
            var toUser = await _context.Users.FindAsync(to);
            if (fromUser is null || toUser is null || !fromUser.Friends.Contains(to))
                return false;

            var roomKey = gameType.ToRoomKey(code);

            var toInvitation = new ToInvitationToGame(to, roomKey);   // from = sender
            var fromInvitation = new FromInvitationToGame(from, roomKey); // to = receiver

            fromUser.OutcomingInviteToGameRequests.Add(toInvitation);
            toUser.IncomingInviteToGameRequests.Add(fromInvitation);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveInviteFriendToGame(string from, string to, string gameType, string code)
        {
            var fromUser = await _context.Users.FindAsync(from);
            var toUser = await _context.Users.FindAsync(to);
            if (fromUser is null || toUser is null)
                return false;

            var roomKey = gameType.ToRoomKey(code);


            Console.WriteLine(from);
            Console.WriteLine(to);
            Console.WriteLine(gameType);
            Console.WriteLine(code);

            var toInvitation = new ToInvitationToGame(to, roomKey);
            var fromInvitation = new FromInvitationToGame(from, roomKey);

            Console.WriteLine(JsonSerializer.Serialize(fromInvitation));
            Console.WriteLine(JsonSerializer.Serialize(toUser.IncomingInviteToGameRequests[0]));

            Console.WriteLine(JsonSerializer.Serialize(toInvitation));
            Console.WriteLine(JsonSerializer.Serialize(fromUser.OutcomingInviteToGameRequests[0]));

            Console.WriteLine(fromUser.OutcomingInviteToGameRequests.Remove(toInvitation));
            Console.WriteLine(toUser.IncomingInviteToGameRequests.Remove(fromInvitation));

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<User?> RemoveInviteFriendToGameExpired(string username, RoomService roomService)
        {
            var user = await _context.Users
                .Include(u => u.IncomingInviteToGameRequests)
                .Include(u => u.OutcomingInviteToGameRequests)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user is null)
                return null;

            var removedAny = false;

            // Handle incoming invitations
            var expiredIncoming = user.IncomingInviteToGameRequests
                .Where(inv => !RoomExistsInMemory(inv.RoomKey, roomService))
                .ToList();

            

            foreach (var inv in expiredIncoming)
            {
                user.IncomingInviteToGameRequests.Remove(inv);
                removedAny = true;

                var sender = await _context.Users
                    .Include(u => u.OutcomingInviteToGameRequests)
                    .FirstOrDefaultAsync(u => u.Username == inv.FromUsername);

                if (sender is not null)
                {
                    var match = sender.OutcomingInviteToGameRequests
                        .FirstOrDefault(o => o.RoomKey == inv.RoomKey && o.ToUsername == username);

                    if (match is not null)
                        sender.OutcomingInviteToGameRequests.Remove(match);
                }
            }

            // Handle outcoming invitations
            var expiredOutcoming = user.OutcomingInviteToGameRequests
                .Where(inv => !RoomExistsInMemory(inv.RoomKey, roomService))
                .ToList();

            foreach (var inv in expiredOutcoming)
            {
                user.OutcomingInviteToGameRequests.Remove(inv);
                removedAny = true;

                var receiver = await _context.Users
                    .Include(u => u.IncomingInviteToGameRequests)
                    .FirstOrDefaultAsync(u => u.Username == inv.ToUsername);

                if (receiver is not null)
                {
                    var match = receiver.IncomingInviteToGameRequests
                        .FirstOrDefault(i => i.RoomKey == inv.RoomKey && i.FromUsername == username);

                    if (match is not null)
                        receiver.IncomingInviteToGameRequests.Remove(match);
                }
            }

            if (removedAny)
                await _context.SaveChangesAsync();

            return user;
        }
    
        private bool RoomExistsInMemory(string roomKey, RoomService roomService)
        {
            var parts = roomKey.Split(':');
            if (parts.Length != 2) return false;

            var gameType = parts[0];
            var code = parts[1];

            return roomService.RoomExists(gameType, code);
        }

        public async Task<bool> ClearAllInvitesAsync(string username)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null)
                return false;

            // Clear all incoming/outgoing invites
            user.IncomingInviteToGameRequests.Clear();
            user.OutcomingInviteToGameRequests.Clear();

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

        // PATCH: Add GetUserByUsername method for ActiveSessionController
        public User? GetUserByUsername(string username)
        {
            // Use EF Core context to find user by username
            return _context.Users.FirstOrDefault(u => u.Username == username);
        }

        // New: apply a game result to update MMR and win streaks
        public async Task<bool> ApplyGameResultAsync(string gameType, string? winnerUsername, string? loserUsername, bool isDraw = false)
        {
            if (isDraw) return true; // no changes for draw

            if (string.IsNullOrWhiteSpace(winnerUsername) || string.IsNullOrWhiteSpace(loserUsername))
                return false;

            var winner = await _context.Users.FindAsync(winnerUsername);
            var loser = await _context.Users.FindAsync(loserUsername);
            if (winner == null || loser == null) return false;

            const int baseWinDelta = 15;
            const int loseDelta = 10;
            const int perStreakBonus = 2; // +2 MMR for each current win streak

            switch (gameType)
            {
                case "rock-paper-scissors":
                {
                    var bonus = perStreakBonus * winner.RockPaperScissorsWinStreak;
                    winner.RockPaperScissorsMMR += baseWinDelta + bonus;
                    loser.RockPaperScissorsMMR = Math.Max(0, loser.RockPaperScissorsMMR - loseDelta);
                    winner.RockPaperScissorsWinStreak += 1;
                    loser.RockPaperScissorsWinStreak = 0;
                    break;
                }
                case "four-in-a-row":
                {
                    var bonus = perStreakBonus * winner.FourInARowWinStreak;
                    winner.FourInARowMMR += baseWinDelta + bonus;
                    loser.FourInARowMMR = Math.Max(0, loser.FourInARowMMR - loseDelta);
                    winner.FourInARowWinStreak += 1;
                    loser.FourInARowWinStreak = 0;
                    break;
                }
                case "pair-matching":
                {
                    var bonus = perStreakBonus * winner.PairMatchingWinStreak;
                    winner.PairMatchingMMR += baseWinDelta + bonus;
                    loser.PairMatchingMMR = Math.Max(0, loser.PairMatchingMMR - loseDelta);
                    winner.PairMatchingWinStreak += 1;
                    loser.PairMatchingWinStreak = 0;
                    break;
                }
                default:
                    return false;
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}

