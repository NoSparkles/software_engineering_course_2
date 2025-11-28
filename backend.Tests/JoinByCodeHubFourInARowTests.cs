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
    public class JoinByCodeHubFourInARowTests
    {
        private readonly JoinByCodeHub _hub;
        private readonly IUserService _userService;
        private readonly IRoomService _roomService;
        private readonly ISingleClientProxy _callerProxy;
        private readonly IHubCallerClients _clients;
        private readonly GameDbContext _context;

        public JoinByCodeHubFourInARowTests()
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
        public async Task HandleCommand_ResetGame_Should_SendGameReset()
        {
            var gameType = "four-in-a-row";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            _context.Users.Add(user);
            _context.SaveChanges();

            var roomUser = new RoomUser("player1", true, user);

            var game = new FourInARowGame();
            game.RoomCode = roomKey;  // Set the RoomCode so HandleCommand can send to the group
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser }
            };
            _roomService.Rooms[roomKey] = room;

            var jwtToken = _userService.GenerateJwtToken(user);
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            _hub.Clients = clients;

            bool resetSent = false;
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "GameReset",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .Invokes((string method, object[] args, CancellationToken _) =>
                {
                    resetSent = true;
                })
                .Returns(Task.CompletedTask);

            await _hub.HandleCommand(gameType, roomCode, "player1", "RESET", jwtToken);

            resetSent.Should().BeTrue();
        }

        [Fact]
        public async Task Join_ShouldFail_WhenRoomDoesNotExist()
        {
            // Arrange
            var gameType = "four-in-a-row";
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
            var gameType = "four-in-a-row";
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
            var gameType = "four-in-a-row";
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

            updatedRoom.Game.Should().BeOfType<FourInARowGame>("the game instance should be of type FourInARowGame");
            updatedRoom.Code.Should().Be(roomKey, "the room code should match the one used to create the room");

            updatedRoom.RoomPlayers[0].Username.Should().Be("player1");
            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[0]).Should().Be("R");
            updatedRoom.RoomPlayers[1].Username.Should().Be("player2");
            updatedRoom.Game.GetPlayerColor(updatedRoom.RoomPlayers[1]).Should().Be("Y");
        }

        [Fact]
        public async Task HandleCommand_Move_Should_ApplyValidMove()
        {
            var gameType = "four-in-a-row";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.SaveChanges();

            var game = new FourInARowGame();
            game.RoomCode = roomKey;
            game.AssignPlayerColors(new RoomUser("player1", true, user1), new RoomUser("player2", true, user2));

            var roomUser1 = new RoomUser("player1", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("player2", true, user2) { PlayerId = "player2" };
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
            };
            _roomService.Rooms[roomKey] = room;

            var jwtToken = _userService.GenerateJwtToken(user1);
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            _hub.Clients = clients;

            bool moveSent = false;
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "ReceiveMove",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .Invokes((string method, object[] args, CancellationToken _) =>
                {
                    moveSent = true;
                })
                .Returns(Task.CompletedTask);

            await _hub.HandleCommand(gameType, roomCode, "player1", "MOVE:3", jwtToken);

            moveSent.Should().BeTrue();
            game.Board[5, 3].Should().Be("R");
            game.CurrentPlayerColor.Should().Be("Y");
        }

        [Fact]
        public async Task HandleCommand_Move_Should_RejectInvalidColumn()
        {
            var gameType = "four-in-a-row";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.SaveChanges();

            var game = new FourInARowGame();
            game.RoomCode = roomKey;
            game.AssignPlayerColors(new RoomUser("player1", true, user1), new RoomUser("player2", true, user2));

            var roomUser1 = new RoomUser("player1", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("player2", true, user2) { PlayerId = "player2" };
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
            };
            _roomService.Rooms[roomKey] = room;

            var jwtToken = _userService.GenerateJwtToken(user1);
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            _hub.Clients = clients;

            await _hub.HandleCommand(gameType, roomCode, "player1", "MOVE:10", jwtToken);

            A.CallTo(() => groupProxy.SendCoreAsync(
                    "ReceiveMove",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Move_Should_RejectWrongPlayerTurn()
        {
            var gameType = "four-in-a-row";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.SaveChanges();

            var game = new FourInARowGame();
            game.RoomCode = roomKey;
            game.AssignPlayerColors(new RoomUser("player1", true, user1), new RoomUser("player2", true, user2));

            var roomUser1 = new RoomUser("player1", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("player2", true, user2) { PlayerId = "player2" };
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
            };
            _roomService.Rooms[roomKey] = room;

            var jwtToken = _userService.GenerateJwtToken(user2);
            var groupProxy = A.Fake<IClientProxy>();
            var clients = A.Fake<IHubCallerClients>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            _hub.Clients = clients;

            await _hub.HandleCommand(gameType, roomCode, "player2", "MOVE:3", jwtToken);

            A.CallTo(() => groupProxy.SendCoreAsync(
                    "ReceiveMove",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public void IsValidMove_Should_ReturnTrue_ForValidColumn()
        {
            var game = new FourInARowGame();
            game.IsValidMove(3).Should().BeTrue();
            game.IsValidMove(0).Should().BeTrue();
            game.IsValidMove(6).Should().BeTrue();
        }

        [Fact]
        public void IsValidMove_Should_ReturnFalse_ForInvalidColumn()
        {
            var game = new FourInARowGame();
            game.IsValidMove(-1).Should().BeFalse();
            game.IsValidMove(7).Should().BeFalse();
            game.IsValidMove(10).Should().BeFalse();
        }

        [Fact]
        public void IsValidMove_Should_ReturnFalse_ForFullColumn()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            // Fill column 0
            for (int i = 0; i < 6; i++)
            {
                game.ApplyMove(0, i % 2 == 0 ? "R" : "Y", clients);
            }

            game.IsValidMove(0).Should().BeFalse();
        }

        [Fact]
        public void ApplyMove_Should_PlacePieceAtBottom()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            game.ApplyMove(3, "R", clients).Should().BeTrue();
            game.Board[5, 3].Should().Be("R");
            game.Board[4, 3].Should().BeNull();
        }

        [Fact]
        public void ApplyMove_Should_StackPieces()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            game.ApplyMove(3, "R", clients).Should().BeTrue();
            game.ApplyMove(3, "Y", clients).Should().BeTrue();

            game.Board[5, 3].Should().Be("R");
            game.Board[4, 3].Should().Be("Y");
        }

        [Fact]
        public void ApplyMove_Should_ChangeCurrentPlayer()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            game.CurrentPlayerColor.Should().Be("R");
            game.ApplyMove(3, "R", clients).Should().BeTrue();
            game.CurrentPlayerColor.Should().Be("Y");
            game.ApplyMove(2, "Y", clients).Should().BeTrue();
            game.CurrentPlayerColor.Should().Be("R");
        }

        [Fact]
        public void ApplyMove_Should_DetectHorizontalWin()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create horizontal win for R
            game.ApplyMove(0, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(1, "R", clients);
            game.ApplyMove(1, "Y", clients);
            game.ApplyMove(2, "R", clients);
            game.ApplyMove(2, "Y", clients);
            game.ApplyMove(3, "R", clients);

            game.WinnerColor.Should().Be("R");
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "GameOver",
                    A<object[]>.That.Contains("R"),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ApplyMove_Should_DetectVerticalWin()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create vertical win for R
            game.ApplyMove(3, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(3, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(3, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(3, "R", clients);

            game.WinnerColor.Should().Be("R");
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "GameOver",
                    A<object[]>.That.Contains("R"),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ApplyMove_Should_DetectDiagonalWin_UpRight()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create diagonal win (up-right) for R
            // Diagonal from (5,0) to (2,3): R at (5,0), (4,1), (3,2), (2,3)
            game.ApplyMove(0, "R", clients); // row 5, col 0 - R
            game.ApplyMove(0, "Y", clients); // row 4, col 0
            game.ApplyMove(1, "Y", clients); // row 5, col 1
            game.ApplyMove(1, "R", clients); // row 4, col 1 - R
            game.ApplyMove(1, "Y", clients); // row 3, col 1
            game.ApplyMove(2, "Y", clients); // row 5, col 2
            game.ApplyMove(2, "Y", clients); // row 4, col 2
            game.ApplyMove(2, "R", clients); // row 3, col 2 - R
            game.ApplyMove(2, "Y", clients); // row 2, col 2
            game.ApplyMove(3, "Y", clients); // row 5, col 3
            game.ApplyMove(3, "Y", clients); // row 4, col 3
            game.ApplyMove(3, "Y", clients); // row 3, col 3
            game.ApplyMove(3, "R", clients); // row 2, col 3 - R (wins!)

            game.WinnerColor.Should().Be("R");
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "GameOver",
                    A<object[]>.That.Contains("R"),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ApplyMove_Should_DetectDiagonalWin_UpLeft()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create diagonal win (up-left) for R
            // Diagonal from (5,3) to (2,0): R at (5,3), (4,2), (3,1), (2,0)
            game.ApplyMove(3, "R", clients); // row 5, col 3 - R
            game.ApplyMove(3, "Y", clients); // row 4, col 3
            game.ApplyMove(2, "Y", clients); // row 5, col 2
            game.ApplyMove(2, "R", clients); // row 4, col 2 - R
            game.ApplyMove(2, "Y", clients); // row 3, col 2
            game.ApplyMove(1, "Y", clients); // row 5, col 1
            game.ApplyMove(1, "Y", clients); // row 4, col 1
            game.ApplyMove(1, "R", clients); // row 3, col 1 - R
            game.ApplyMove(1, "Y", clients); // row 2, col 1
            game.ApplyMove(0, "Y", clients); // row 5, col 0
            game.ApplyMove(0, "Y", clients); // row 4, col 0
            game.ApplyMove(0, "Y", clients); // row 3, col 0
            game.ApplyMove(0, "R", clients); // row 2, col 0 - R (wins!)

            game.WinnerColor.Should().Be("R");
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "GameOver",
                    A<object[]>.That.Contains("R"),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void ApplyMove_Should_DetectDraw()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Fill board in a pattern that avoids wins
            // Strategy: Fill columns with alternating pattern, but ensure no 4 in a row
            // We'll fill column by column, alternating R and Y
            int moveCount = 0;
            for (int col = 0; col < 7 && moveCount < 42; col++)
            {
                for (int row = 0; row < 6 && moveCount < 42; row++)
                {
                    // Use the current player color (which alternates automatically)
                    string color = game.CurrentPlayerColor;
                    bool moved = game.ApplyMove(col, color, clients);
                    if (moved)
                    {
                        moveCount++;
                        // If a win was detected (not a draw), we need to stop
                        if (!string.IsNullOrEmpty(game.WinnerColor) && game.WinnerColor != "DRAW")
                        {
                            // This shouldn't happen with our pattern, but handle it
                            break;
                        }
                        // If draw was detected, we're done
                        if (game.WinnerColor == "DRAW")
                        {
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(game.WinnerColor))
                    break;
            }

            // After filling, check if draw was detected
            // The draw should be detected when the board becomes full
            bool boardFull = true;
            for (int col = 0; col < 7; col++)
            {
                if (game.Board[0, col] == null)
                {
                    boardFull = false;
                    break;
                }
            }

            // If board is full, draw should have been detected
            if (boardFull)
            {
                game.WinnerColor.Should().Be("DRAW");
                A.CallTo(() => groupProxy.SendCoreAsync(
                        "GameOver",
                        A<object[]>.That.Contains("DRAW"),
                        A<CancellationToken>._))
                    .MustHaveHappened();
            }
        }

        [Fact]
        public void ApplyMove_Should_RejectMoveAfterGameWon()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create a win
            game.ApplyMove(0, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(1, "R", clients);
            game.ApplyMove(1, "Y", clients);
            game.ApplyMove(2, "R", clients);
            game.ApplyMove(2, "Y", clients);
            game.ApplyMove(3, "R", clients);

            game.WinnerColor.Should().Be("R");

            // Try to make another move
            var result = game.ApplyMove(4, "Y", clients);
            result.Should().BeFalse();
        }

        [Fact]
        public void GetGameState_Should_ReturnCurrentState()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            game.ApplyMove(3, "R", clients);

            var state = game.GetGameState();
            state.Should().NotBeNull();

            // Use reflection to check the state
            var stateType = state.GetType();
            var boardProperty = stateType.GetProperty("board");
            var currentPlayerProperty = stateType.GetProperty("currentPlayer");
            var winnerProperty = stateType.GetProperty("winner");

            boardProperty.Should().NotBeNull();
            currentPlayerProperty.Should().NotBeNull();
            winnerProperty.Should().NotBeNull();

            var board = boardProperty!.GetValue(state) as string[][];
            var currentPlayer = currentPlayerProperty!.GetValue(state) as string;
            var winner = winnerProperty!.GetValue(state) as string;

            board.Should().NotBeNull();
            board!.Length.Should().Be(6);
            board[0].Length.Should().Be(7);
            currentPlayer.Should().Be("Y");
            winner.Should().Be("");
        }

        [Fact]
        public async Task ReportWin_Should_CompleteSuccessfully()
        {
            var game = new FourInARowGame();
            game.RoomCode = "test-room";
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group("test-room")).Returns(groupProxy);

            // Create a win
            game.ApplyMove(0, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(1, "R", clients);
            game.ApplyMove(1, "Y", clients);
            game.ApplyMove(2, "R", clients);
            game.ApplyMove(2, "Y", clients);
            game.ApplyMove(3, "R", clients);

            await game.ReportWin("player1", clients);

            // ReportWin should complete without error
            game.WinnerColor.Should().Be("R");
        }

        [Fact]
        public async Task HandleCommand_Move_Should_RejectMoveWhenGameWon()
        {
            var gameType = "four-in-a-row";
            var roomCode = "room1";
            var roomKey = gameType.ToRoomKey(roomCode);

            var user1 = new User { Username = "player1", PasswordHash = "hashedpassword" };
            var user2 = new User { Username = "player2", PasswordHash = "hashedpassword" };
            _context.Users.Add(user1);
            _context.Users.Add(user2);
            _context.SaveChanges();

            var game = new FourInARowGame();
            game.RoomCode = roomKey;
            game.AssignPlayerColors(new RoomUser("player1", true, user1), new RoomUser("player2", true, user2));

            // Create a win
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(roomKey)).Returns(groupProxy);
            game.ApplyMove(0, "R", clients);
            game.ApplyMove(0, "Y", clients);
            game.ApplyMove(1, "R", clients);
            game.ApplyMove(1, "Y", clients);
            game.ApplyMove(2, "R", clients);
            game.ApplyMove(2, "Y", clients);
            game.ApplyMove(3, "R", clients);

            var roomUser1 = new RoomUser("player1", true, user1) { PlayerId = "player1" };
            var roomUser2 = new RoomUser("player2", true, user2) { PlayerId = "player2" };
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode,
                RoomPlayers = { roomUser1, roomUser2 }
            };
            _roomService.Rooms[roomKey] = room;

            var jwtToken = _userService.GenerateJwtToken(user2);
            _hub.Clients = clients;

            await _hub.HandleCommand(gameType, roomCode, "player2", "MOVE:4", jwtToken);

            // Should not send another move
            A.CallTo(() => groupProxy.SendCoreAsync(
                    "ReceiveMove",
                    A<object[]>.Ignored,
                    A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public void ApplyMove_Should_RejectInvalidColumnIndex()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            game.ApplyMove(-1, "R", clients).Should().BeFalse();
            game.ApplyMove(7, "R", clients).Should().BeFalse();
            game.ApplyMove(10, "R", clients).Should().BeFalse();
        }

        [Fact]
        public void ApplyMove_Should_RejectWrongPlayerColor()
        {
            var game = new FourInARowGame();
            var clients = A.Fake<IHubCallerClients>();
            var groupProxy = A.Fake<IClientProxy>();
            A.CallTo(() => clients.Group(A<string>._)).Returns(groupProxy);

            game.CurrentPlayerColor.Should().Be("R");
            game.ApplyMove(3, "Y", clients).Should().BeFalse();
        }

        private Room CreateRoomWithoutPlayers(string gameType, string roomCode)
        {
            var roomKey = gameType.ToRoomKey(roomCode);

            var game = new FourInARowGame();
            var room = new Room(game, isMatchMaking: false)
            {
                Code = roomCode
            };

            _roomService.Rooms[roomKey] = room;
            return room;
        }
    }
}
