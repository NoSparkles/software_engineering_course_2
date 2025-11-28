using Models;

namespace Services
{
    public interface IUserService
    {
        Task<User> GetUserAsync(string username);
        Task<List<User>> SearchUsersAsync(string query);
        Task<bool> RegisterUserAsync(string username, string password);
        string GenerateJwtToken(User user);
        Task<User?> GetUserFromTokenAsync(string jwtToken);
        Task<bool> DeleteUserAsync(string username);
        Task<bool> SendFriendRequestAsync(string username, string targetUsername);
        Task<bool> AcceptFriendRequestAsync(string username, string requesterUsername);
        Task<bool> RejectFriendRequestAsync(string username, string requesterUsername);
        Task<bool> RemoveFriendAsync(string username, string friendUsername);
        Task<bool> InviteFriendToGame(string from, string to, string gameType, string code);
        Task<bool> RemoveInviteFriendToGame(string from, string to, string gameType, string code);
        Task<User?> RemoveInviteFriendToGameExpired(string username, IRoomService roomService);
        Task<bool> ClearAllInvitesAsync(string username);
        Task<bool> UpdateMMRAsync(string username, Dictionary<string, int> mmrUpdates);
        Task<List<User>> GetAllUsersAsync();
        string HashPassword(string password);
        User? GetUserByUsername(string username);
        Task<bool> ApplyGameResultAsync(string gameType, string? winnerUsername, string? loserUsername, bool isDraw = false);
    }
}

