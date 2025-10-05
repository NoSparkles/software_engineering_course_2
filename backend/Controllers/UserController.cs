using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

namespace Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<User>> GetMe()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username))
                return Unauthorized("Missing username claim.");

            var user = await _userService.GetUserAsync(username);
            if (user == null)
                return NotFound("User not found.");

            return Ok(user);
        }

        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMe()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(username))
                return Unauthorized("Missing username claim.");

            var success = await _userService.DeleteUserAsync(username);
            if (!success)
                return NotFound("User not found or could not be deleted.");

            return NoContent();
        }

        // GET: User/{username}
        [HttpGet("{username}")]
        public async Task<ActionResult<User>> GetUser(string username)
        {
            var user = await _userService.GetUserAsync(username);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        // POST: /User/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] RegisterDto registerDto)
        {
            if (string.IsNullOrWhiteSpace(registerDto.Username) || string.IsNullOrWhiteSpace(registerDto.Password))
                return BadRequest("Username and password are required.");

            var success = await _userService.RegisterUserAsync(registerDto.Username, registerDto.Password);
            if (!success)
                return Conflict("Username already exists.");

            var createdUser = await _userService.GetUserAsync(registerDto.Username);
            return CreatedAtAction(nameof(GetUser), new { username = registerDto.Username }, createdUser);
        }

        // POST: /User/login
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginDto loginDto)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
                return BadRequest("Username and password are required.");

            var user = await _userService.GetUserAsync(loginDto.Username);
            if (user == null)
                return Unauthorized("Invalid username or password.");

            var hashedInput = _userService.HashPassword(loginDto.Password);
            if (user.PasswordHash != hashedInput)
                return Unauthorized("Invalid username or password.");

            var token = _userService.GenerateJwtToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Username,
                    user.Friends,
                    user.RockPaperScissorsMMR,
                    user.FourInARowMMR,
                    user.PairMatchingMMR,
                    user.RockPaperScissorsWinStreak,
                    user.FourInARowWinStreak,
                    user.PairMatchingWinStreak,
                    user.IncomingFriendRequests,
                    user.OutgoingFriendRequests
                }
            });
        }

        // PUT: User/{username}/send-request
        [HttpPut("{username}/send-request")]
        public async Task<ActionResult> SendFriendRequest(string username, [FromBody] string targetUsername)
        {
            var success = await _userService.SendFriendRequestAsync(username, targetUsername);
            if (!success)
                return BadRequest("Could not send friend request.");

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(new
            {
                updatedUser.Friends,
                updatedUser.IncomingFriendRequests,
                updatedUser.OutgoingFriendRequests
            });
        }

        // PUT: User/{username}/accept-request
        [HttpPut("{username}/accept-request")]
        public async Task<ActionResult> AcceptFriendRequest(string username, [FromBody] string requesterUsername)
        {
            var success = await _userService.AcceptFriendRequestAsync(username, requesterUsername);
            if (!success)
                return BadRequest("Could not accept friend request.");

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(new
            {
                updatedUser.Friends,
                updatedUser.IncomingFriendRequests,
                updatedUser.OutgoingFriendRequests
            });
        }

        // PUT: User/{username}/reject-request
        [HttpPut("{username}/reject-request")]
        public async Task<ActionResult> RejectFriendRequest(string username, [FromBody] string requesterUsername)
        {
            var success = await _userService.RejectFriendRequestAsync(username, requesterUsername);
            if (!success)
                return BadRequest("Could not reject friend request.");

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(new
            {
                updatedUser.Friends,
                updatedUser.IncomingFriendRequests,
                updatedUser.OutgoingFriendRequests
            });
        }

        // PUT: User/{username}/remove-friend
        [HttpPut("{username}/remove-friend")]
        public async Task<ActionResult> RemoveFriend(string username, [FromBody] string friendUsername)
        {
            var success = await _userService.RemoveFriendAsync(username, friendUsername);
            if (!success)
                return NotFound("User or friend not found.");

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(updatedUser?.Friends);
        }

        // PUT: User/{username}/update-mmr
        [HttpPut("{username}/update-mmr")]
        public async Task<ActionResult> UpdateMMR(string username, [FromBody] Dictionary<string, int> mmrUpdates)
        {
            var success = await _userService.UpdateMMRAsync(username, mmrUpdates);
            if (!success)
                return NotFound();

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(updatedUser);
        }

        // GET: User
        [HttpGet]
        public async Task<ActionResult<List<User>>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }
    }

    public class RegisterDto
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class LoginDto
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
