using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;
using Services;

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

        [Fact]
        public async Task GetUserAsync_Should_Return_User_When_Found()
        {
            var user = new User { Username = "robert", PasswordHash = "hash123" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _service.GetUserAsync("robert");

            result.Should().NotBeNull();
            result!.Username.Should().Be("robert");
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
    }
}