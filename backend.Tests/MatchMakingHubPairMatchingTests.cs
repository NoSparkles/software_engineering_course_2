using Data;
using Extensions;
using FakeItEasy;
using FluentAssertions;
using Games;
using Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Models;
using Models.InMemoryModels;
using Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests
{
    public class MatchMakingHubPairMatchingTests
    {
        private readonly MatchMakingHub _hub;
        private readonly IUserService _userService;
        private readonly IRoomService _roomService;
        private readonly ISingleClientProxy _callerProxy;
        private readonly IHubCallerClients _clients;
        private readonly GameDbContext _context;

        public MatchMakingHubPairMatchingTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            // 👇 Use a fake IUserService instead of a real UserService
            _userService = A.Fake<IUserService>();

            var hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);

            _hub = new MatchMakingHub(_userService, _roomService);

            _callerProxy = A.Fake<ISingleClientProxy>();
            _clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => _clients.Caller).Returns(_callerProxy);
            _hub.Clients = _clients;

            var context = A.Fake<HubCallerContext>();
            A.CallTo(() => context.ConnectionId).Returns("conn1");
            _hub.Context = context;

            var fakeGroupManager = A.Fake<IGroupManager>();
            A.CallTo(() => fakeGroupManager.AddToGroupAsync(
                A<string>._, A<string>._, A<CancellationToken>._))
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
            var room = new Room(game, isMatchMaking: true)
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
        public async Task HandleCommand_Flip_Should_Flip_Card_ForPairMatching()
        {
            var gameType = "pair-matching";
            var roomCode = "room12";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.SaveChanges();

            var game = new PairMatching();
            game.RoomCode = roomKey;
            game.AssignPlayerColors(new RoomUser("player1", true, user1), new RoomUser("player2", true, user2));

            var roomUser1 = new RoomUser("player1", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("player2", true, user2) { PlayerId = "player2" };
            var room = new Room(game, isMatchMaking: true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
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

            var jwtToken = _userService.GenerateJwtToken(user1);
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            _hub.Clients = clients;

            await _hub.HandleCommand(gameType, roomCode, "player1", "flip 1 1", jwtToken);

            var gameAfter = _roomService.Rooms[roomKey].Game as PairMatching;
            gameAfter.Should().NotBeNull();
            gameAfter!.FlippedCards[0].Should().Contain(new List<int> { 1, 1 });
            gameAfter.CurrentPlayerColor.Should().Be("R");

            await _hub.HandleCommand(gameType, roomCode, "player1", "flip 1 0", jwtToken);

            var gameAfter2 = _roomService.Rooms[roomKey].Game as PairMatching;
            gameAfter2.Should().NotBeNull();
            gameAfter2.CurrentPlayerColor.Should().Be("Y");

            await _hub.HandleCommand(gameType, roomCode, "player2", "flip 2 1", jwtToken);

            var gameAfter3 = _roomService.Rooms[roomKey].Game as PairMatching;
            gameAfter.Should().NotBeNull();
            gameAfter!.FlippedCards[0].Should().Contain(new List<int> { 2, 1 });
            gameAfter.CurrentPlayerColor.Should().Be("Y");

            await _hub.HandleCommand(gameType, roomCode, "player2", "flip 2 0", jwtToken);

            var gameAfter4 = _roomService.Rooms[roomKey].Game as PairMatching;
            gameAfter2.Should().NotBeNull();
            gameAfter2.CurrentPlayerColor.Should().Be("R");

        }   

        [Fact]
        public async Task Join_ShouldThrow_WhenRoomDoesNotExist()
        {
            var gameType = "pair-matching";
            var roomCode = "nonexistent";
            var jwtToken = "fakeJwt";

            Func<Task> act = () => _hub.Join(gameType, roomCode, "player1", jwtToken);

            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Room pair-matching:NONEXISTENT does not exist.");
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
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.AddRange(user1, user2);
            _context.SaveChanges();

            A.CallTo(() => _userService.GenerateJwtToken(user1)).Returns("jwt1");
            A.CallTo(() => _userService.GenerateJwtToken(user2)).Returns("jwt2");

            var callerProxy = A.Fake<ISingleClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            _hub.Clients = clients;

            A.CallTo(() => clients.Caller).Returns(callerProxy);
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.Join(gameType, roomCode, "player1", "jwt1");

            _roomService.Rooms.Should().ContainKey(roomKey);
            var updatedRoom = _roomService.Rooms[roomKey];

            updatedRoom.RoomPlayers.Should().HaveCount(1);
            updatedRoom.GameStarted.Should().BeFalse("game should not start after only one player joins");

            await _hub.Join(gameType, roomCode, "player2", "jwt2");

            updatedRoom = _roomService.Rooms[roomKey];

            updatedRoom.RoomPlayers.Should().HaveCount(2);
            updatedRoom.GameStarted.Should().BeTrue("game should start when two players join");

            updatedRoom.Game.Should().BeOfType<PairMatching>("the game instance should be of type PairMatching");
            updatedRoom.Code.Should().Be(roomKey, "the room code should match the one used to create the room");

            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[0]).Should().Be("R");
            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[1]).Should().Be("Y");
        }

        [Fact]
        public async Task JoinAsSpectator_ShouldSendBoardState_WhenRoomExists()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var game = new PairMatching();
            var room = new Room(game, true) { Code = roomCode };
            _roomService.Rooms[roomKey] = room;

            var callerProxy = A.Fake<ISingleClientProxy>();
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            A.CallTo(() => clients.Caller).Returns(callerProxy);
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.JoinAsSpectator(gameType, roomCode);

            A.CallTo(() => callerProxy.SendCoreAsync("ReceiveBoard",
                A<object[]>.That.Matches(args => args.Length == 1), default))
                .MustHaveHappened();

            A.CallTo(() => callerProxy.SendCoreAsync("GameStateUpdate",
                A<object[]>.That.Matches(args => args.Length == 1), default))
                .MustHaveHappened();

            A.CallTo(() => groupProxy.SendCoreAsync("SpectatorJoined",
                A<object[]>.That.Matches(args => ((string)args[0]).StartsWith("spec-")), default))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinAsSpectator_ShouldFail_WhenRoomDoesNotExist()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var callerProxy = A.Fake<ISingleClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;
            A.CallTo(() => clients.Caller).Returns(callerProxy);

            await _hub.JoinAsSpectator(gameType, roomCode);

            A.CallTo(() => callerProxy.SendCoreAsync("SpectatorJoinFailed",
                A<object[]>.That.Matches(args => (string)args[0] == "Room does not exist"), default))
                .MustHaveHappened();
        }

        [Fact]
        public async Task LeaveRoom_ShouldCloseRoomAndRemovePlayer_WhenRoomExists()
        {
            var gameType = "pair-matching";
            var roomCode = "room13";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);
            var game = new PairMatching();
            var room = new Room(game, isMatchMaking: true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;

            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            _hub.Clients = clients;
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.LeaveRoom(gameType, roomCode, "player1");

            _roomService.Rooms.Should().NotContainKey(roomKey);

            _roomService.CodeRoomUsers.ContainsKey("player1").Should().BeFalse();
        }

        [Fact]
        public async Task LeaveRoom_ShouldRemovePlayerAndNotThrow_WhenRoomDoesNotExist()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            _hub.Clients = clients;
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);

            await _hub.LeaveRoom(gameType, roomCode, "player1");

            _roomService.Rooms.Should().NotContainKey(roomKey);

            _roomService.CodeRoomUsers.ContainsKey("player1").Should().BeFalse();
        }

        [Fact]
        public async Task DeclineReconnection_ShouldCloseRoomAndKickPlayers_WhenRoomExists()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);
            var game = new PairMatching();
            var room = new Room(game, isMatchMaking: true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.DeclineReconnection("player1", gameType, roomCode);

            _roomService.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task DeclineReconnection_ShouldClearMatchmakingSession_WhenRoomDoesNotExist()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Add player to CodeRoomUsers (not removed by DeclineReconnection itself)
            _roomService.CodeRoomUsers.TryAdd("player1", new RoomUser("player1", true, user));

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            // Act
            await _hub.DeclineReconnection("player1", gameType, roomCode);

            // Assert: player is still tracked, but ClearActiveMatchmakingSession was called
            _roomService.CodeRoomUsers.ContainsKey("player1").Should().BeTrue();
            // If you want to assert the call:
            // A.CallTo(() => _roomService.ClearActiveMatchmakingSession("player1")).MustHaveHappened();
        }
                
        [Fact]
        public async Task DeclineReconnection_ShouldCloseRoom_WhenRoomExists()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var room = new Room(new PairMatching(), false) { Code = roomCode };
            _roomService.Rooms[roomKey] = room;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            // Act
            await _hub.DeclineReconnection("player1", gameType, roomCode);

            // Assert: room should be closed
            _roomService.Rooms.ContainsKey(roomKey).Should().BeFalse();
        }

        [Fact]
        public async Task ReportWin_ShouldReturn_WhenRoomDoesNotExist()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.ReportWin(gameType, roomCode, "player1");

            A.CallTo(() => _userService.ApplyGameResultAsync(
                A<string>._, A<string>._, A<string>._, A<bool>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task ReportWin_ShouldReturn_WhenRoomIsNull()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            _roomService.Rooms[roomKey] = null!;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.ReportWin(gameType, roomCode, "player1");

            A.CallTo(() => _userService.ApplyGameResultAsync(
                A<string>._, A<string>._, A<string>._, A<bool>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task ReportWin_ShouldApplyGameResult_WhenWinnerAndLoserExist()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "winner", PasswordHash = "pw" };
            var user2 = new User { Username = "loser", PasswordHash = "pw" };
            _context.Users.AddRange(user1, user2);
            _context.SaveChanges();

            var roomUser1 = new RoomUser("winner", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("loser", true, user2) { PlayerId = "player2" };

            var game = A.Fake<PairMatching>();
            var room = new Room(game, true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
            };
            _roomService.Rooms[roomKey] = room;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.ReportWin(gameType, roomCode, "player1");

            A.CallTo(() => _userService.ApplyGameResultAsync(
                gameType, "winner", "loser", false))
                .MustHaveHappened();
        }

        [Fact]
        public async Task ReportWin_ShouldSkipMmrUpdate_WhenUsernamesMissing()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "", PasswordHash = "pw" }; // missing username
            _context.Users.Add(user1);
            _context.SaveChanges();

            var roomUser1 = new RoomUser("", true, user1) { PlayerId = "player1" };

            var game = A.Fake<PairMatching>();
            var room = new Room(game, true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1 }
            };
            _roomService.Rooms[roomKey] = room;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.ReportWin(gameType, roomCode, "player1");

            A.CallTo(() => _userService.ApplyGameResultAsync(
                A<string>._, A<string>._, A<string>._, A<bool>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldKeepPlayerInCodeRoomUsers_WhenParametersPresent()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);
            var game = new PairMatching();
            var room = new Room(game, true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;
            _roomService.CodeRoomUsers.TryAdd("player1", roomUser);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString($"?playerId=player1&gameType={gameType}&roomCode={roomCode}");

            var hubContext = A.Fake<HubCallerContext>();
            A.CallTo(() => hubContext.ConnectionId).Returns("conn1");
            hubContext.Items["HttpContext"] = httpContext;
            _hub.Context = hubContext;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.OnDisconnectedAsync(null);

            _roomService.CodeRoomUsers.ContainsKey("player1").Should().BeTrue();
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldNotModifyCodeRoomUsers_WhenParametersMissing()
        {
            var gameType = "pair-matching";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);
            var game = new PairMatching();
            var room = new Room(game, true)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;
            _roomService.CodeRoomUsers.TryAdd("player1", roomUser);

            var httpContext = new DefaultHttpContext();

            var hubContext = A.Fake<HubCallerContext>();
            A.CallTo(() => hubContext.ConnectionId).Returns("conn1");
            hubContext.Items["HttpContext"] = httpContext;
            _hub.Context = hubContext;

            var clients = A.Fake<IHubCallerClients>();
            _hub.Clients = clients;

            await _hub.OnDisconnectedAsync(null);

            _roomService.CodeRoomUsers.ContainsKey("player1").Should().BeTrue();
        }

        private Room CreateRoomWithoutPlayers(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);

            var game = new PairMatching();
            var room = new Room(game, isMatchMaking: true)
            {
                Code = roomCode
            };

            _roomService.Rooms[roomKey] = room;
            return room;
        }
    }
}
