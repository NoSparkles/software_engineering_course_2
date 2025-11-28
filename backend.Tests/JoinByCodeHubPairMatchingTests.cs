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
        private readonly IUserService _userService;
        private readonly IRoomService _roomService;
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

            var fakeGroupManager = A.Fake<IGroupManager>();
        A.CallTo(() => fakeGroupManager.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        _hub.Groups = fakeGroupManager;

        }

        [Fact]
        public async Task HandleCommand_GetBoard_Should_SendInitialBoard_ForPairMatching()
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

        [Fact]
        public async Task Join_ShouldFail_WhenRoomDoesNotExist()
        {
            // Arrange
            var gameType = "pair-matching";
            var roomCode = "nonexistent";
            var jwtToken = "fakeJwt";

            var callerProxy = A.Fake<ISingleClientProxy>();
            A.CallTo(() => _clients.Caller).Returns(callerProxy);

            await _hub.Join(gameType, roomCode, "player1", jwtToken);

            A.CallTo(() => callerProxy.SendCoreAsync(
                "JoinFailed",
                A<object[]>.That.Contains("Room no longer exists. It may have been closed."),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Join_ShouldAddPlayerToRoom_WhenRoomExists()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var room = CreateRoomWithoutPlayers(gameType, roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var jwtToken = _userService.GenerateJwtToken(user);

            var callerProxy = A.Fake<ISingleClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            _hub.Clients = clients;
            A.CallTo(() => clients.Caller).Returns(callerProxy);
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.Join(gameType, roomCode, "player1", jwtToken);

            _roomService.Rooms.Should().ContainKey(roomKey);

            var updatedRoom = _roomService.Rooms[roomKey];

            var playerExists = updatedRoom.RoomPlayers.Exists(rp => rp.Username == "player1");

            playerExists.Should().BeTrue("JoinAsPlayerNotMatchMaking should be called to add player to the room");
            
            _roomService.Rooms.Should().ContainKey(roomKey);
        }

        [Fact]
        public async Task Join_ShouldStartGame_WhenTwoPlayersJoinRoom()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var room = CreateRoomWithoutPlayers(gameType, roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);

            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user2);
            _context.SaveChanges();

            var jwt1 = _userService.GenerateJwtToken(user1);
            var jwt2 = _userService.GenerateJwtToken(user2);

            var callerProxy = A.Fake<ISingleClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            _hub.Clients = clients;

            A.CallTo(() => clients.Caller).Returns(callerProxy);
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.Join(gameType, roomCode, "player1", jwt1);

            _roomService.Rooms.Should().ContainKey(roomKey);
            var updatedRoom = _roomService.Rooms[roomKey];

            updatedRoom.RoomPlayers.Should().HaveCount(1);
            updatedRoom.GameStarted.Should().BeFalse("game should not start after only one player joins");

            await _hub.Join(gameType, roomCode, "player2", jwt2);

            updatedRoom = _roomService.Rooms[roomKey];

            updatedRoom.RoomPlayers.Should().HaveCount(2);
            updatedRoom.GameStarted.Should().BeTrue("game should start when two players join");

            updatedRoom.Game.Should().BeOfType<PairMatching>("the game instance should be of type PairMatching");
            updatedRoom.Code.Should().Be(roomKey, "the room code should match the one used to create the room");

            updatedRoom.RoomPlayers[0].Username.Should().Be("player1");
            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[0]).Should().Be("R");
            updatedRoom.RoomPlayers[1].Username.Should().Be("player2");
            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[1]).Should().Be("Y");
        }

        private Room CreateRoomWithoutPlayers(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);

            var game = new PairMatching();
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode
            };

            _roomService.Rooms[roomKey] = room;
            return room;
        }
    }
}
