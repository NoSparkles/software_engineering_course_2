using System.Security.Claims;
using Controllers;
using Controllers.Dtos;
using Data;
using FakeItEasy;
using FluentAssertions;
using Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Models;
using Services;

namespace backend.Tests
{
    public class UserControllerTests
    {
        private readonly GameDbContext _context;
        private readonly UserService _service;
        private readonly RoomService _roomService;
        private readonly UserController _controller;

        public UserControllerTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            IHubContext<SpectatorHub> hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);

            _service = new UserService(_context);

            _controller = new UserController(_service, _roomService);
        }

        // Helper to mock authenticated user
        private void SetUserIdentity(string username)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username)
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = principal }
            };
        }

        [Fact]
        public async Task GetMe_Should_Return_Ok_When_User_Exists()
        {
            _context.Users.Add(new User { Username = "test", PasswordHash = "h" });
            await _context.SaveChangesAsync();

            SetUserIdentity("test");

            var result = await _controller.GetMe();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var u = Assert.IsType<User>(ok.Value);

            u.Username.Should().Be("test");
        }

        [Fact]
        public async Task GetMe_Should_Return_Unauthorized_When_No_Claim()
        {
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
            };

            var result = await _controller.GetMe();
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);

            unauthorized.Value.Should().Be("Missing username claim.");
        }

        [Fact]
        public async Task GetMe_Should_Return_NotFound_When_Service_Throws()
        {
            SetUserIdentity("ghost");

            var result = await _controller.GetMe();
            var nf = Assert.IsType<NotFoundObjectResult>(result.Result);

            nf.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteMe_Should_Return_NoContent_When_Deleted()
        {
            _context.Users.Add(new User { Username = "del", PasswordHash = "xx" });
            await _context.SaveChangesAsync();

            SetUserIdentity("del");

            var result = await _controller.DeleteMe();
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteMe_Should_Return_NotFound_When_No_User()
        {
            SetUserIdentity("nouser");

            var result = await _controller.DeleteMe();
            var nf = Assert.IsType<NotFoundObjectResult>(result);

            nf.Value.Should().Be("User not found or could not be deleted.");
        }

        [Fact]
        public async Task DeleteMe_Should_Return_Unauthorized_When_No_Claim()
        {
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
            };

            var result = await _controller.DeleteMe();
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

            unauthorized.Value.Should().Be("Missing username claim.");
        }

        [Fact]
        public async Task GetUser_Should_Return_NotFound_When_Not_Exist()
        {
            var result = await _controller.GetUser("none");
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task SearchUsers_Should_Return_BadRequest_When_Empty()
        {
            var result = await _controller.SearchUsers("");
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);

            bad.Value.Should().Be("Query cannot be empty.");
        }

        [Fact]
        public async Task Login_Should_Return_Unauthorized_When_UserNotFound_Exception()
        {
            var dto = new LoginDto { Username = "x", Password = "y" };
            var result = await _controller.Login(dto);

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task SendFriendRequest_Should_Return_Ok_When_Successful()
        {
            _context.Users.Add(new User { Username = "a", PasswordHash = "x" });
            _context.Users.Add(new User { Username = "b", PasswordHash = "x" });
            await _context.SaveChangesAsync();

            var result = await _controller.SendFriendRequest("a", "b");
            var ok = Assert.IsType<OkObjectResult>(result);

            ok.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task SendFriendRequest_Should_Return_BadRequest_When_Fails()
        {
            var result = await _controller.SendFriendRequest("ghost", "ghost2");
            var bad = Assert.IsType<BadRequestObjectResult>(result);

            bad.Value.Should().Be("Could not send friend request.");
        }

        [Fact]
        public async Task AcceptFriendRequest_Should_Return_Ok_When_Successful()
        {
            _context.Users.Add(new User { Username = "aa", PasswordHash = "x" });
            _context.Users.Add(new User { Username = "bb", PasswordHash = "x" });
            await _context.SaveChangesAsync();

            await _controller.SendFriendRequest("bb", "aa");

            var result = await _controller.AcceptFriendRequest("aa", "bb");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task AcceptFriendRequest_Should_Return_BadRequest_When_Fails()
        {
            var result = await _controller.AcceptFriendRequest("x", "y");
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RejectFriendRequest_Should_Return_Ok()
        {
            _context.Users.Add(new User { Username = "u", PasswordHash = "x" });
            _context.Users.Add(new User { Username = "v", PasswordHash = "x" });
            await _context.SaveChangesAsync();

            await _controller.SendFriendRequest("v", "u");

            var result = await _controller.RejectFriendRequest("u", "v");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RemoveFriend_Should_Return_NotFound_When_Fails()
        {
            var result = await _controller.RemoveFriend("x", "y");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task InviteFriendToGame_Should_Return_NotFound_When_Fails()
        {
            var dto = new InvitationDto { Username = "a", GameType = "rock-paper-scissors", Code = "123" };
            var result = await _controller.InviteFriendToGame("b", dto);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task ClearAllInvites_Should_Return_NotFound_When_Fails()
        {
            var result = await _controller.ClearAllInvites("ghost");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task RemoveExpiredInvitations_Should_Return_NotFound_When_No_User()
        {
            var result = await _controller.RemoveExpiredInvitations("ghost");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdateMMR_Should_Return_NotFound_When_Fails()
        {
            var mmr = new Dictionary<string, int>() { { "rps", 10 } };

            var result = await _controller.UpdateMMR("ghost", mmr);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetAllUsers_Should_Return_Empty_When_None()
        {
            var result = await _controller.GetAllUsers();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<User>>(ok.Value);

            list.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllUsers_Should_Return_List_When_Exists()
        {
            _context.Users.Add(new User { Username = "x", PasswordHash = "x" });
            await _context.SaveChangesAsync();

            var result = await _controller.GetAllUsers();
            var ok = Assert.IsType<OkObjectResult>(result.Result);

            var list = Assert.IsType<List<User>>(ok.Value);
            list.Should().HaveCount(1);
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Not_InGame()
        {
            var result = _controller.GetUserCurrentGame("ghost");
            var ok = Assert.IsType<OkObjectResult>(result.Result);

            dynamic val = ok.Value!;
            bool inGame = (bool)val.GetType().GetProperty("inGame")!.GetValue(val);

            Assert.False(inGame);
        }

        [Fact]
        public async Task Register_Should_Return_Created_When_Valid()
        {
            var dto = new RegisterDto { Username = "newuser", Password = "password123" };
            var result = await _controller.Register(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var user = Assert.IsType<User>(created.Value);
            user.Username.Should().Be("newuser");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Username_Is_Null()
        {
            var dto = new RegisterDto { Username = null!, Password = "password123" };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Username_Is_Empty()
        {
            var dto = new RegisterDto { Username = "", Password = "password123" };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Username_Is_Whitespace()
        {
            var dto = new RegisterDto { Username = "   ", Password = "password123" };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Password_Is_Null()
        {
            var dto = new RegisterDto { Username = "newuser", Password = null! };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Password_Is_Empty()
        {
            var dto = new RegisterDto { Username = "newuser", Password = "" };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Password_Is_Whitespace()
        {
            var dto = new RegisterDto { Username = "newuser", Password = "   " };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_BadRequest_When_Both_Are_Null()
        {
            var dto = new RegisterDto { Username = null!, Password = null! };
            var result = await _controller.Register(dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            badRequest.Value.Should().Be("Username and password are required.");
        }

        [Fact]
        public async Task Register_Should_Return_Conflict_When_Username_Already_Exists()
        {
            _context.Users.Add(new User { Username = "existing", PasswordHash = "hash" });
            await _context.SaveChangesAsync();

            var dto = new RegisterDto { Username = "existing", Password = "password123" };
            var result = await _controller.Register(dto);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            conflict.Value.Should().Be("Username already exists.");
        }

        [Fact]
        public async Task Register_Should_Create_User_With_Correct_Username()
        {
            var dto = new RegisterDto { Username = "testuser", Password = "testpass" };
            var result = await _controller.Register(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var user = Assert.IsType<User>(created.Value);
            user.Username.Should().Be("testuser");
            
            // Verify user was actually saved to database
            var savedUser = await _context.Users.FindAsync("testuser");
            savedUser.Should().NotBeNull();
            savedUser!.Username.Should().Be("testuser");
        }

        [Fact]
        public async Task Register_Should_Hash_Password()
        {
            var dto = new RegisterDto { Username = "hashtest", Password = "plaintext" };
            var result = await _controller.Register(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var user = Assert.IsType<User>(created.Value);
            
            // Password should be hashed, not plaintext
            user.PasswordHash.Should().NotBe("plaintext");
            user.PasswordHash.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void RegisterDto_Should_Have_Default_Values()
        {
            var dto = new RegisterDto();
            dto.Username.Should().BeNull();
            dto.Password.Should().BeNull();
        }

        [Fact]
        public void RegisterDto_Should_Allow_Property_Assignment()
        {
            var dto = new RegisterDto
            {
                Username = "test",
                Password = "pass"
            };

            dto.Username.Should().Be("test");
            dto.Password.Should().Be("pass");
        }

        [Fact]
        public void RegisterDto_Should_Support_Record_Equality()
        {
            var dto1 = new RegisterDto { Username = "user", Password = "pass" };
            var dto2 = new RegisterDto { Username = "user", Password = "pass" };
            var dto3 = new RegisterDto { Username = "user", Password = "different" };

            // Records use value equality
            dto1.Should().Be(dto2);
            dto1.Should().NotBe(dto3);
        }

    }
}
