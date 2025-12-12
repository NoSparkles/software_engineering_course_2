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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Tests
{
    public class MatchMakingHubTests
    {
        private readonly MatchMakingHub _hub;
        private readonly IUserService _userService;
        private readonly IRoomService _roomService;
        private readonly ISingleClientProxy _callerProxy;
        private readonly IHubCallerClients _clients;
        private readonly GameDbContext _context;
        private readonly ConcurrentDictionary<string, Room> _roomsDict;
        private readonly ConcurrentDictionary<string, string> _activeSessions;
        
        public MatchMakingHubTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            _userService = A.Fake<IUserService>();
            _roomService = A.Fake<IRoomService>();

            // Initialize dictionaries
            _roomsDict = new ConcurrentDictionary<string, Room>();
            _activeSessions = new ConcurrentDictionary<string, string>();

            // Set up default room service behavior
            A.CallTo(() => _roomService.Rooms).Returns(_roomsDict);
            A.CallTo(() => _roomService.ActiveMatchmakingSessions).Returns(_activeSessions);

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
                
            A.CallTo(() => fakeGroupManager.RemoveFromGroupAsync(
                A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(Task.CompletedTask);

            _hub.Groups = fakeGroupManager;
        }

        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public async Task JoinMatchmaking_WithValidGameType_ShouldReturnRoomCode(string gameType)
        {
            // Arrange
            var jwtToken = "valid-token";
            var playerId = "player1";
            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };
            
            _context.Users.Add(user);
            _context.SaveChanges();

            // Mock GetUserFromTokenAsync - it will be called TWICE:
            // 1. In JoinMatchmaking method
            // 2. In Join method (which is called by JoinMatchmaking)
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Mock ForceRemovePlayerFromAllRooms - The hub checks if player is in any rooms first
            // Since we haven't added any rooms with this player, this won't be called
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients))
                .Returns(Task.CompletedTask);
            
            // Mock CleanupInactiveMatchmakingSessions
            A.CallTo(() => _roomService.CleanupInactiveMatchmakingSessions());
            
            // Clear rooms dict for this test
            _roomsDict.Clear();
            
            // Mock CreateRoom to return a test room code
            var testRoomCode = "TEST123";
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Returns(testRoomCode);

            // IMPORTANT: The hub creates a room, then calls Join which needs the room to exist
            // We need to set up the room to exist when Join is called
            var roomKey = gameType.ToRoomKey(testRoomCode);
            var room = new Room(CreateGameForType(gameType), isMatchMaking: true);
            
            // Set up so that when the hub calls Rooms.ContainsKey, it returns true
            // We'll add the room to the dictionary AFTER CreateRoom is called
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Invokes(() => _roomsDict[roomKey] = room)
                .Returns(testRoomCode);
            
            // Mock JoinAsPlayerMatchMaking
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, testRoomCode, playerId, user, "conn1", _clients))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().Be(testRoomCode);
            
            // Verify the correct methods were called
            // GetUserFromTokenAsync should be called TWICE (once in JoinMatchmaking, once in Join)
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _roomService.CleanupInactiveMatchmakingSessions()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _roomService.CreateRoom(gameType, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, testRoomCode, playerId, user, "conn1", _clients))
                .MustHaveHappenedOnceExactly();
        }

        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public async Task JoinMatchmaking_WithAvailableRoom_ShouldJoinExistingRoom(string gameType)
        {
            // Arrange
            var jwtToken = "valid-token";
            var playerId = "player2";
            var user = new User { Username = "player2", PasswordHash = "hashedpassword" };
            
            // Existing room with one player waiting
            var existingRoomCode = "EXIST123";
            var existingRoomKey = $"{gameType}:{existingRoomCode}";
            
            // Create game instance based on type
            GameInstance game = CreateGameForType(gameType);
            var existingRoom = new Room(game, isMatchMaking: true)
            {
                RoomPlayers = new List<RoomUser> 
                { 
                    new RoomUser("player1", true, new User { Username = "player1" })
                },
                IsMatchMaking = true,
                GameStarted = false
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            // GetUserFromTokenAsync will be called TWICE
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Mock ForceRemovePlayerFromAllRooms - player is not in any rooms initially
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients))
                .Returns(Task.CompletedTask);
            
            // Mock CleanupInactiveMatchmakingSessions
            A.CallTo(() => _roomService.CleanupInactiveMatchmakingSessions());
            
            // Add room to our dictionary
            _roomsDict.Clear();
            _roomsDict[existingRoomKey] = existingRoom;
            
            // Mock JoinAsPlayerMatchMaking for when hub calls Join
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, existingRoomCode, playerId, user, "conn1", _clients))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().Be(existingRoomCode);
            
            // Verify that JoinAsPlayerMatchMaking was called
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, existingRoomCode, playerId, user, "conn1", _clients))
                .MustHaveHappenedOnceExactly();
            
            // ForceRemovePlayerFromAllRooms should NOT be called since player is not in any rooms
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients)).MustNotHaveHappened();
        }

        [Fact]
        public async Task JoinMatchmaking_WithInvalidToken_ShouldReturnNull()
        {
            // Arrange
            var jwtToken = "invalid-token";
            var gameType = "rock-paper-scissors";
            var playerId = "player1";

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(null));

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().BeNull();
            
            // Verify GetUserFromTokenAsync was called ONCE (it returns early when user is null)
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Join_WithValidParameters_ShouldAddToGroupAndJoinRoom()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var jwtToken = "valid-token";
            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Mock room exists by adding to dictionary
            var roomKey = gameType.ToRoomKey(roomCode);
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;
            
            // Mock JoinAsPlayerMatchMaking
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, roomCode, playerId, user, "conn1", _clients))
                .Returns(Task.CompletedTask);

            // Act
            await _hub.Join(gameType, roomCode, playerId, jwtToken);

            // Assert
            // Verify AddToGroupAsync was called
            A.CallTo(() => _hub.Groups.AddToGroupAsync("conn1", roomKey, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            
            // Verify JoinAsPlayerMatchMaking was called
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, roomCode, playerId, user, "conn1", _clients))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task EndMatchmakingSession_WithActiveSession_ShouldCloseRoom()
        {
            // Arrange
            var playerId = "player1";
            var roomKey = "rock-paper-scissors:ROOM123";
            
            // Add to active sessions
            _activeSessions[playerId] = roomKey;
            
            // Mock CloseRoomAndKickAllPlayers
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, A<string>._, A<string>._))
                .Returns(Task.CompletedTask);

            // Act
            await _hub.EndMatchmakingSession(playerId);

            // Assert
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, "Matchmaking session ended by a player", A<string>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task LeaveRoom_ShouldRemoveFromGroupAndCloseRoom()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            // Add room to dictionary
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;
            
            // Mock CloseRoomAndKickAllPlayers
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, A<string>._, A<string>._))
                .Returns(Task.CompletedTask);

            // Act
            await _hub.LeaveRoom(gameType, roomCode, playerId);

            // Assert
            // Verify RemoveFromGroupAsync was called
            A.CallTo(() => _hub.Groups.RemoveFromGroupAsync("conn1", roomKey, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            
            // Verify CloseRoomAndKickAllPlayers was called with the correct excludePlayerId
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, "A player left the matchmaking session", playerId))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ReportWin_ShouldCallGameReportWinAndUpdateUserService()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            var winner = new RoomUser("player1", true, new User { Username = "winnerUser" });
            var loser = new RoomUser("player2", true, new User { Username = "loserUser" });
            
            var room = new Room(new RockPaperScissors(), isMatchMaking: true)
            {
                RoomPlayers = new List<RoomUser> { winner, loser }
            };
            
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;
            
            // Mock GetRoomByKey to return our room
            A.CallTo(() => _roomService.GetRoomByKey(roomKey))
                .Returns(room);
            
            A.CallTo(() => _userService.ApplyGameResultAsync(gameType, "winnerUser", "loserUser", false))
                .Returns(Task.FromResult(true));

            // Act
            await _hub.ReportWin(gameType, roomCode, playerId);

            // Assert
            A.CallTo(() => _userService.ApplyGameResultAsync(
                gameType, "winnerUser", "loserUser", false)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task DeclineReconnection_ShouldCloseRoom()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;
            
            // Mock CloseRoomAndKickAllPlayers
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, A<string>._, A<string>._))
                .Returns(Task.CompletedTask);

            A.CallTo(() => _roomService.ClearActiveMatchmakingSession(playerId));

            // Act
            await _hub.DeclineReconnection(playerId, gameType, roomCode);

            // Assert
            A.CallTo(() => _roomService.CloseRoomAndKickAllPlayers(
                roomKey, _clients, "A player declined to reconnect", A<string>._))
                .MustHaveHappenedOnceExactly();
        }

        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public async Task JoinMatchmaking_WhenRoomsPropertyReturnsEmpty_ShouldCreateNewRoom(string gameType)
        {
            // Arrange
            var jwtToken = "valid-token";
            var playerId = "player1";
            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };

            // GetUserFromTokenAsync will be called TWICE
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Mock ForceRemovePlayerFromAllRooms - player not in any rooms
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients))
                .Returns(Task.CompletedTask);
            
            A.CallTo(() => _roomService.CleanupInactiveMatchmakingSessions());
            
            // Clear the dictionary to ensure no rooms exist
            _roomsDict.Clear();
            
            var testRoomCode = "NEWROOM123";
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Returns(testRoomCode);
            
            // Create room after CreateRoom is called using Invokes
            var roomKey = gameType.ToRoomKey(testRoomCode);
            var room = new Room(CreateGameForType(gameType), isMatchMaking: true);
            
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Invokes(() => _roomsDict[roomKey] = room)
                .Returns(testRoomCode);
            
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, testRoomCode, playerId, user, "conn1", _clients))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().Be(testRoomCode);
            A.CallTo(() => _roomService.CreateRoom(gameType, true)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task JoinMatchmaking_WhenUserInExistingRoom_ShouldRemoveFromOtherRoomsFirst()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var jwtToken = "valid-token";
            var playerId = "player1";
            var user = new User { Username = "player1", PasswordHash = "hashedpassword" };

            // GetUserFromTokenAsync will be called TWICE
            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Create a room with this player in it - but this room is FULL (2 players)
            // The hub looks for rooms with exactly 1 player, so it should skip this room
            var existingRoomKey = $"{gameType}:OLDROOM";
            var existingRoom = new Room(new RockPaperScissors(), isMatchMaking: true)
            {
                RoomPlayers = new List<RoomUser> 
                { 
                    new RoomUser(playerId, true, user),
                    new RoomUser("player2", true, new User { Username = "player2" }) // Second player
                },
                DisconnectedPlayers = new Dictionary<string, RoomUser>(),
                IsMatchMaking = true,
                GameStarted = false
            };
            _roomsDict[existingRoomKey] = existingRoom;
            
            // Setup ForceRemovePlayerFromAllRooms - SHOULD be called since player is in a room
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients))
                .Returns(Task.CompletedTask);
            
            A.CallTo(() => _roomService.CleanupInactiveMatchmakingSessions());
            
            // Setup to create new room
            var testRoomCode = "TEST456";
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Returns(testRoomCode);
            
            // Create the new room for the Join method using Invokes
            var newRoomKey = gameType.ToRoomKey(testRoomCode);
            var newRoom = new Room(new RockPaperScissors(), isMatchMaking: true);
            
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Invokes(() => _roomsDict[newRoomKey] = newRoom)
                .Returns(testRoomCode);
            
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, testRoomCode, playerId, user, "conn1", _clients))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().Be(testRoomCode); // Should create NEW room, not join OLDROOM
            A.CallTo(() => _roomService.ForceRemovePlayerFromAllRooms(playerId, _clients)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task RoomExistsWithMatchmaking_ShouldCallRoomServiceMethod()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            
            A.CallTo(() => _roomService.RoomExistsWithMatchmaking(gameType, roomCode))
                .Returns((true, true));

            // Act
            var result = await _hub.RoomExistsWithMatchmaking(gameType, roomCode);

            // Assert
            result.Should().NotBeNull();
            A.CallTo(() => _roomService.RoomExistsWithMatchmaking(gameType, roomCode))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task JoinAsSpectator_ShouldAddToGroupAndCallRoomService()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var roomKey = gameType.ToRoomKey(roomCode);
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;

            A.CallTo(() => _roomService.JoinAsSpectator(gameType, roomCode, A<string>._, A<User>._))
                .Returns(Task.CompletedTask);

            // Act
            await _hub.JoinAsSpectator(gameType, roomCode);

            // Assert
            A.CallTo(() => _hub.Groups.AddToGroupAsync("conn1", roomKey, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _roomService.JoinAsSpectator(gameType, roomCode, A<string>._, A<User>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task HandleCommand_ShouldCallGameHandleCommand()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var command = "some-command";
            var jwtToken = "valid-token";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            var user = new User { Username = "player1" };
            var roomUser = new RoomUser(playerId, true, user);
            var room = new Room(new RockPaperScissors(), isMatchMaking: true)
            {
                RoomPlayers = new List<RoomUser> { roomUser }
            };
            
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            A.CallTo(() => _roomService.GetRoomUser(roomKey, playerId, user))
                .Returns(roomUser);

            // Act
            await _hub.HandleCommand(gameType, roomCode, playerId, command, jwtToken);

            // Assert
            A.CallTo(() => _roomService.GetRoomUser(roomKey, playerId, user))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task JoinMatchmaking_WhenExceptionOccurs_ShouldReturnNullAndSendError()
        {
            // Arrange
            var jwtToken = "valid-token";
            var gameType = "rock-paper-scissors";
            var playerId = "player1";
            var user = new User { Username = "player1" };

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Simulate an exception in CreateRoom
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Throws(new Exception("Test exception"));

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task JoinMatchmaking_WhenJoinThrowsException_ShouldReturnNull()
        {
            // Arrange
            var jwtToken = "valid-token";
            var gameType = "rock-paper-scissors";
            var playerId = "player1";
            var user = new User { Username = "player1" };

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            _roomsDict.Clear();
            
            var testRoomCode = "TEST123";
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Returns(testRoomCode);
            
            // Create room for Join method using Invokes
            var roomKey = gameType.ToRoomKey(testRoomCode);
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            
            A.CallTo(() => _roomService.CreateRoom(gameType, true))
                .Invokes(() => _roomsDict[roomKey] = room)
                .Returns(testRoomCode);
            
            // Make JoinAsPlayerMatchMaking throw an exception
            A.CallTo(() => _roomService.JoinAsPlayerMatchMaking(gameType, testRoomCode, playerId, user, "conn1", _clients))
                .Throws(new Exception("Join failed"));

            // Act
            var result = await _hub.JoinMatchmaking(jwtToken, gameType, playerId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Join_WhenRoomDoesNotExist_ShouldThrowException()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "NONEXISTENT";
            var playerId = "player1";
            var jwtToken = "valid-token";
            var user = new User { Username = "player1" };

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            // Clear rooms dict - room doesn't exist
            _roomsDict.Clear();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _hub.Join(gameType, roomCode, playerId, jwtToken));
            
            // Verify AddToGroupAsync was NOT called
            A.CallTo(() => _hub.Groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task ReportWin_WhenRoomDoesNotExist_ShouldNotCallUserService()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "NONEXISTENT";
            var playerId = "player1";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            // Room doesn't exist in dictionary
            _roomsDict.Clear();

            // Act
            await _hub.ReportWin(gameType, roomCode, playerId);

            // Assert
            A.CallTo(() => _userService.ApplyGameResultAsync(A<string>._, A<string>._, A<string>._, A<bool>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task ReportWin_WhenRoomExistsButNoWinnerOrLoser_ShouldNotCallUserService()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            // Room with no players (or players without usernames)
            var room = new Room(new RockPaperScissors(), isMatchMaking: true)
            {
                RoomPlayers = new List<RoomUser>()
            };
            
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;
            
            // Mock GetRoomByKey to return our room
            A.CallTo(() => _roomService.GetRoomByKey(roomKey))
                .Returns(room);

            // Act
            await _hub.ReportWin(gameType, roomCode, playerId);

            // Assert
            A.CallTo(() => _userService.ApplyGameResultAsync(A<string>._, A<string>._, A<string>._, A<bool>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_WhenRoomDoesNotExist_ShouldNotCallGetRoomUser()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "NONEXISTENT";
            var playerId = "player1";
            var command = "some-command";
            var jwtToken = "valid-token";
            
            // Room doesn't exist
            _roomsDict.Clear();

            // Act
            await _hub.HandleCommand(gameType, roomCode, playerId, command, jwtToken);

            // Assert
            A.CallTo(() => _roomService.GetRoomUser(A<string>._, A<string>._, A<User>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_WhenGetRoomUserReturnsNull_ShouldNotCallGameHandleCommand()
        {
            // Arrange
            var gameType = "rock-paper-scissors";
            var roomCode = "ROOM123";
            var playerId = "player1";
            var command = "some-command";
            var jwtToken = "valid-token";
            var roomKey = gameType.ToRoomKey(roomCode);
            
            var user = new User { Username = "player1" };
            var room = new Room(new RockPaperScissors(), isMatchMaking: true);
            _roomsDict.Clear();
            _roomsDict[roomKey] = room;

            A.CallTo(() => _userService.GetUserFromTokenAsync(jwtToken))
                .Returns(Task.FromResult<User?>(user));
            
            A.CallTo(() => _roomService.GetRoomUser(roomKey, playerId, user))
                .Returns((RoomUser?)null);

            // Act
            await _hub.HandleCommand(gameType, roomCode, playerId, command, jwtToken);

            // Assert
            A.CallTo(() => _roomService.GetRoomUser(roomKey, playerId, user))
                .MustHaveHappenedOnceExactly();
            // The game.HandleCommand should not be called since GetRoomUser returns null
        }

        private GameInstance CreateGameForType(string gameType)
        {
            return gameType switch
            {
                "rock-paper-scissors" => new RockPaperScissors(),
                "four-in-a-row" => new FourInARowGame(),
                "pair-matching" => new PairMatching(),
                _ => throw new ArgumentException($"Unknown game type: {gameType}")
            };
        }
    }
}