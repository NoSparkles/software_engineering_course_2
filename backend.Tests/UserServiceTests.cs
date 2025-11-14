using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;
using Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace backend.Tests
{
    public class UserServiceTests
    {
        private readonly GameDbContext _context;
        private readonly UserService _service;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _service = new UserService(_context);
        }

        [Theory]
        [InlineData("robert", "hash123")]
        [InlineData("alice", "hash456")]
        [InlineData("bob", "hash789")]
        public async Task GetUserAsync_Should_Return_User_When_Found(string username, string passwordHash)
        {
            // Arrange
            var user = new User { Username = username, PasswordHash = passwordHash };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetUserAsync(username);

            // Assert
            result.Should().NotBeNull();
            result!.Username.Should().Be(username);
            result.PasswordHash.Should().Be(passwordHash);
        }

        [Fact]
        public async Task SearchUsersAsync_Should_Return_Matching_Users()
        {
            _context.Users.AddRange(
                new User { Username = "robert", PasswordHash = "hash123" },
                new User { Username = "robocop", PasswordHash = "hash123" },
                new User { Username = "alice", PasswordHash = "hash123" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.SearchUsersAsync("rob");

            result.Should().HaveCount(2);
            result.Select(u => u.Username).Should().Contain(new[] { "robert", "robocop" });
        }

        [Fact]
        public async Task RegisterUserAsync_Should_Add_User_When_Not_Exists()
        {
            var result = await _service.RegisterUserAsync("newuser", "password123");

            result.Should().BeTrue();
            var user = await _context.Users.FindAsync("newuser");
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task RegisterUserAsync_Should_Return_False_When_User_Already_Exists()
        {
            var existingUser = new User
            {
                Username = "existinguser",
                PasswordHash = "hash123"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var result = await _service.RegisterUserAsync("existinguser", "newpassword");

            result.Should().BeFalse();

            var usersWithSameUsername = await _context.Users.CountAsync(u => u.Username == "existinguser");
            usersWithSameUsername.Should().Be(1);
        }

        [Fact]
        public void GenerateJwtToken_Should_Return_Valid_Jwt_For_User()
        {
            var user = new User { Username = "robert", PasswordHash = "hash123" };

            var tokenString = _service.GenerateJwtToken(user);

            tokenString.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            var nameClaim = token.Claims.FirstOrDefault(c => c.Type == "unique_name");
            nameClaim.Should().NotBeNull();
            nameClaim!.Value.Should().Be("robert");

            token.ValidTo.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_User_When_Token_Is_Valid()
        {
            var username = "robert";
            var passwordHash = "hash123";
            var user = new User { Username = username, PasswordHash = passwordHash };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _service.GenerateJwtToken(user);

            var result = await _service.GetUserFromTokenAsync(token);

            result.Should().NotBeNull();
            result!.Username.Should().Be(username);
            result.PasswordHash.Should().Be(passwordHash);
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_Null_When_Token_Has_Invalid_Signature()
        {
            var fakeKey = Encoding.ASCII.GetBytes("this-is-a-very-long-fake-secret-key-123!");
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "robert") }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(fakeKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            var result = await _service.GetUserFromTokenAsync(jwtToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_Null_When_Token_Has_No_Username_Claim()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(UserService.KEY), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            var result = await _service.GetUserFromTokenAsync(jwtToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DeleteUserAsync_Should_Return_True_When_User_Exists()
        {
            _context.Users.Add(new User { Username = "testuser", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.DeleteUserAsync("testuser");

            result.Should().BeTrue();
            var deletedUser = await _context.Users.FindAsync("testuser");
            deletedUser.Should().BeNull();
        }

        [Fact]
        public async Task DeleteUserAsync_Should_Return_False_When_User_Does_Not_Exist()
        {
            var result = await _service.DeleteUserAsync("nonexistent");

            result.Should().BeFalse();
        }

    }
}