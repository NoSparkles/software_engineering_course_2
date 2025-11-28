using System;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FakeItEasy;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Models.InMemoryModels;
using Services;
using Controllers;
using Data;
using Extensions;

namespace backend.Tests
{
    public class SessionControllerTests
    {
        private readonly GameDbContext _context;
        private readonly IRoomService _roomService;
        private readonly SessionController _controller;

        public SessionControllerTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            IHubContext<SpectatorHub> hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);
            _controller = new SessionController(_roomService);
        }

        [Fact]
        public void GetActiveSession_Should_ReturnActiveSession_When_FoundByUsername()
        {
            // Arrange
            var code = _roomService.CreateRoom("pair-matching", false);
            var roomKey = "pair-matching".ToRoomKey(code);
            var room = _roomService.GetRoomByKey(roomKey)!;
            room.RoomCloseTime = DateTime.UtcNow.AddMinutes(5);
            room.RoomPlayers.Add(new RoomUser { PlayerId = "p1", Username = "alice" });

            // Act
            var result = _controller.GetActiveSession(playerId: null, username: "alice") as OkObjectResult;

            // Assert
            result.Should().NotBeNull();
            var value = result!.Value!;
            var activeGameProp = value.GetType().GetProperty("activeGame");
            activeGameProp.Should().NotBeNull();
            var activeGame = activeGameProp.GetValue(value);
            activeGame.Should().NotBeNull();
            var codeProp = activeGame!.GetType().GetProperty("code");
            codeProp!.GetValue(activeGame).Should().Be(room.Code);
            var closeProp = value.GetType().GetProperty("roomCloseTime");
            closeProp!.GetValue(value).Should().NotBeNull();
        }

        [Fact]
        public void GetActiveSession_Should_ReturnActiveSession_When_FoundByPlayerId()
        {
            // Arrange
            var code = _roomService.CreateRoom("four-in-a-row", false);
            var roomKey = "four-in-a-row".ToRoomKey(code);
            var room = _roomService.GetRoomByKey(roomKey)!;
            room.RoomCloseTime = DateTime.UtcNow.AddMinutes(5);
            room.RoomPlayers.Add(new RoomUser { PlayerId = "player42", Username = "bob" });

            // Act
            var result = _controller.GetActiveSession(playerId: "player42", username: null) as OkObjectResult;

            // Assert
            result.Should().NotBeNull();
            var value = result!.Value!;
            var activeGameProp = value.GetType().GetProperty("activeGame");
            activeGameProp.Should().NotBeNull();
            var activeGame = activeGameProp.GetValue(value);
            activeGame.Should().NotBeNull();
            var codeProp = activeGame!.GetType().GetProperty("code");
            codeProp!.GetValue(activeGame).Should().Be(room.Code);
        }

        [Fact]
        public void GetActiveSession_Should_ReturnNullActiveGame_When_NoActiveSession()
        {
            // Arrange - create a room but set close time in the past so it's not active
            var code = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = "rock-paper-scissors".ToRoomKey(code);
            var room = _roomService.GetRoomByKey(roomKey)!;
            room.RoomCloseTime = DateTime.UtcNow.AddMinutes(-5); // expired

            // Act
            var result = _controller.GetActiveSession(playerId: "noone", username: "nobody") as OkObjectResult;

            // Assert
            result.Should().NotBeNull();
            var value = result!.Value!;
            var activeGameProp = value.GetType().GetProperty("activeGame");
            activeGameProp.Should().NotBeNull();
            var activeGame = activeGameProp.GetValue(value);
            activeGame.Should().BeNull();
            var closeProp = value.GetType().GetProperty("roomCloseTime");
            closeProp!.GetValue(value).Should().BeNull();
        }
    }
}