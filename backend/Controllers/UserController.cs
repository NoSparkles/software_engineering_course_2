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

        // GET: api/user/{username}
        [HttpGet("{username}")]
        public async Task<ActionResult<User>> GetUser(string username)
        {
            var user = await _userService.GetUserAsync(username);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        // POST: api/user/register
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

        // PUT: api/user/{username}/add-friend
        [HttpPut("{username}/add-friend")]
        public async Task<ActionResult> AddFriend(string username, [FromBody] string friendUsername)
        {
            var success = await _userService.AddFriendAsync(username, friendUsername);
            if (!success)
                return NotFound("User or friend not found.");

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(updatedUser?.Friends);
        }

        // PUT: api/user/{username}/update-mmr
        [HttpPut("{username}/update-mmr")]
        public async Task<ActionResult> UpdateMMR(string username, [FromBody] Dictionary<string, int> mmrUpdates)
        {
            var success = await _userService.UpdateMMRAsync(username, mmrUpdates);
            if (!success)
                return NotFound();

            var updatedUser = await _userService.GetUserAsync(username);
            return Ok(updatedUser);
        }

        // GET: api/user
        [HttpGet]
        public async Task<ActionResult<List<User>>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }
    }

    // DTO for registration
    public class RegisterDto
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
