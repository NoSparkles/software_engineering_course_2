using Data;
using Extensions;
using FakeItEasy;
using FluentAssertions;
using Games;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.InMemoryModels;
using Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests
{
    public class JoinByCodeHubPairMatchingTests
    {
        private readonly JoinByCodeHub _hub;
        private readonly UserService _userService;
        private readonly RoomService _roomService;
        private readonly ISingleClientProxy _callerProxy;
        private readonly IHubCallerClients _clients;
        private readonly GameDbContext _context;

        public JoinByCodeHubPairMatchingTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            _userService = new UserService(_context);

            var hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);

            _hub = new JoinByCodeHub(_userService, _roomService);

            _callerProxy = A.Fake<ISingleClientProxy>();
            _clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => _clients.Caller).Returns(_callerProxy);
            _hub.Clients = _clients;

            var context = A.Fake<HubCallerContext>();
            A.CallTo(() => context.ConnectionId).Returns("conn1");
            _hub.Context = context;
        }

        [Fact]
        public async Task Join_Should_SendInitialBoard_ForPairMatching()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);

            var game = new PairMatching();
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;

            GameState? sentState = null;
            A.CallTo(() => _callerProxy.SendCoreAsync(
                    "ReceiveBoard",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .Invokes((string method, object[] args, CancellationToken _) =>
                {
                    sentState = args[0] as GameState;
                })
                .Returns(Task.CompletedTask);

            await _hub.HandleCommand(gameType, roomCode, "player1", "getBoard", "fakeJwt");

            sentState.Should().NotBeNull();
            sentState!.Board.Should().HaveCount(18);
            sentState.CurrentPlayer.Should().Be("R");
            sentState.Flipped.Should().BeEmpty();
            sentState.Scores.Should().ContainKey("R");
            sentState.Scores.Should().ContainKey("Y");
            sentState.Winner.Should().BeEmpty();
        }
    }
}
