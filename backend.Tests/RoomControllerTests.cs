using Controllers;
using Data;
using FakeItEasy;
using FluentAssertions;
using Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Services;

namespace backend.Tests {
    public class RoomControllerTests
    {
        private readonly GameDbContext _context;
        private readonly RoomService _roomService;
        private readonly RoomController _controller;
        public RoomControllerTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            IHubContext<SpectatorHub> hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);

            _controller = new RoomController(_roomService);
        }
        
        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public async Task RoomExists_should_return_OkTrue_when_room_exists(string gameType)
        {
            var roomCode = _roomService.CreateRoom(gameType, false);

            var result = await _controller.RoomExists(gameType, roomCode);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().Be(true);
        }

    }
}