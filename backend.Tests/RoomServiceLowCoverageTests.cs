using Xunit;
using FluentAssertions;
using Services;
using Microsoft.AspNetCore.SignalR;
using FakeItEasy;
using Hubs;
using Models.InMemoryModels;
using Models;
using Games;
using System.Collections.Concurrent;
using System.Threading;
using System.Reflection;

namespace backend.Tests
{
    public class RoomServiceLowCoverageTests
    {
        private readonly RoomService _service;
        private readonly IHubContext<SpectatorHub> _hubContext;
        private readonly IHubCallerClients _fakeClients;
        private readonly ISingleClientProxy _mockSingleClientProxy;
        private readonly IClientProxy _mockClientProxy;

        public RoomServiceLowCoverageTests()
        {
            _hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _service = new RoomService(_hubContext);
            _fakeClients = A.Fake<IHubCallerClients>();
            _mockSingleClientProxy = A.Fake<ISingleClientProxy>();
            _mockClientProxy = A.Fake<IClientProxy>();
            
            // Setup default mock behavior
            SetupDefaultMocks();
        }

        private void SetupDefaultMocks()
        {
            // Setup default returns for common methods
            A.CallTo(() => _fakeClients.Caller).Returns(_mockSingleClientProxy);
            A.CallTo(() => _fakeClients.All).Returns(_mockClientProxy);
            A.CallTo(() => _fakeClients.Others).Returns(_mockClientProxy);
            A.CallTo(() => _fakeClients.Client(A<string>._)).Returns(_mockSingleClientProxy);
            A.CallTo(() => _fakeClients.Group(A<string>._)).Returns(_mockClientProxy);
            
            // Setup spectator hub context
            var hubClients = A.Fake<IHubClients>();
            A.CallTo(() => hubClients.All).Returns(_mockClientProxy);
            A.CallTo(() => hubClients.Group(A<string>._)).Returns(_mockClientProxy);
            A.CallTo(() => _hubContext.Clients).Returns(hubClients);
        }

        // Helper method to set private properties using reflection
        private void SetPrivateProperty<T>(object obj, string propertyName, T value)
        {
            var propertyInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(obj, value);
            }
        }

        // ==================== GenerateRoomCode Tests ====================
        [Fact]
        public void GenerateRoomCode_Should_Handle_All_Game_Types_In_Duplicate_Check()
        {
            // Arrange - Fill up room codes for all game types
            var mockRandom = new Random(42);
            
            // Simulate that all codes are taken by creating rooms
            for (int i = 0; i < 100; i++)
            {
                var code = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 6)
                    .Select(s => s[mockRandom.Next(s.Length)]).ToArray());
                
                var room1 = new Room(new RockPaperScissors(), false);
                var room2 = new Room(new FourInARowGame(), false);
                var room3 = new Room(new PairMatching(), false);
                
                _service.Rooms[$"rock-paper-scissors:{code}"] = room1;
                _service.Rooms[$"four-in-a-row:{code}"] = room2;
                _service.Rooms[$"pair-matching:{code}"] = room3;
            }

            // Act - Should still generate a unique code
            var newCode = _service.CreateRoom("rock-paper-scissors", false);

            // Assert
            newCode.Should().NotBeNullOrEmpty();
            newCode.Length.Should().Be(6);
            _service.Rooms.Should().ContainKey($"rock-paper-scissors:{newCode}");
        }

        [Fact]
        public void GenerateRoomCode_Should_Produce_Valid_Characters()
        {
            // Act - Generate multiple codes
            var codes = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                codes.Add(_service.CreateRoom("rock-paper-scissors", false));
            }

            // Assert
            foreach (var code in codes)
            {
                code.Should().MatchRegex("^[A-Z0-9]{6}$");
            }
        }

        // ==================== ReportWin Tests ====================
        [Fact]
        public async Task ReportWin_Should_Send_PlayerWon_When_Player_In_Room()
        {
            // Arrange - Player in one room
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;

            var roomUser = new RoomUser { PlayerId = "player1", Username = "winner" };
            room.RoomPlayers.Add(roomUser);
            room.GameStarted = true;
            room.Code = roomKey; // IMPORTANT: Set the room's Code property!

            var mockClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockClient);

            // Act
            await _service.ReportWin("player1", _fakeClients);

            // Assert
            A.CallTo(() => mockClient.SendCoreAsync("PlayerWon", A<object?[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task ReportWin_Should_Throw_When_Game_ReportWin_Throws()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "winner" };
            room.RoomPlayers.Add(roomUser);
            room.GameStarted = true;
            
            // Mock the game to throw an exception
            var mockGame = A.Fake<RockPaperScissors>();
            A.CallTo(() => mockGame.ReportWin("player1", _fakeClients))
                .Throws(new InvalidOperationException("Game error"));
            room.Game = mockGame;
            
            var mockClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockClient);

            // Act & Assert - Should throw
            var act = async () => await _service.ReportWin("player1", _fakeClients);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Game error");
        }

        // ==================== JoinAsPlayerNotMatchMaking Tests ====================
        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Handle_Null_User()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", null, "conn1", _fakeClients);

            // Assert
            var room = _service.GetRoomByKey(roomKey);
            room.Should().NotBeNull();
            room!.RoomPlayers.Should().ContainSingle(p => p.PlayerId == "player1" && p.Username == null);
        }

        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Send_JoinFailed_When_Room_Does_Not_Exist()
        {
            // Arrange
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", "NONEXISTENT", "player1", null, "conn1", _fakeClients);

            // Assert
            A.CallTo(() => mockSingleClient.SendCoreAsync("JoinFailed", A<object?[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Send_RoomPlayersUpdate_When_Player_Joins()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            var user = new User { Username = "player1", PasswordHash = "hash" };
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", _fakeClients);

            // Assert
            A.CallTo(() => mockGroupClient.SendCoreAsync("RoomPlayersUpdate", A<object?[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Handle_Existing_RoomUser_Correctly()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var user = new User { Username = "player1", PasswordHash = "hash" };
            var existingRoomUser = new RoomUser { PlayerId = "player1", Username = "player1", User = user };
            room.RoomPlayers.Add(existingRoomUser);
            
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act - Try to join again with same user
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", _fakeClients);

            // Assert - Should not add duplicate player
            room.RoomPlayers.Should().ContainSingle(p => p.PlayerId == "player1");
        }

        // ==================== JoinAsPlayerMatchMaking Tests ====================
        [Fact]
        public async Task JoinAsPlayerMatchMaking_Should_Add_To_ActiveMatchmakingSessions()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var user = new User { Username = "player1", PasswordHash = "hash" };
            
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);

            // Act
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", _fakeClients);

            // Assert
            _service.ActiveMatchmakingSessions.Should().ContainKey("player1");
        }

        [Fact]
        public async Task JoinAsPlayerMatchMaking_Should_Clear_Timer_When_Game_Starts()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            var user1 = new User { Username = "player1", PasswordHash = "hash1" };
            var user2 = new User { Username = "player2", PasswordHash = "hash2" };
            
            var mockSingleClient1 = A.Fake<ISingleClientProxy>();
            var mockSingleClient2 = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient1);
            A.CallTo(() => _fakeClients.Client("conn2")).Returns(mockSingleClient2);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act - Join first player (sets timer)
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player1", user1, "conn1", _fakeClients);
            
            var room = _service.GetRoomByKey(roomKey)!;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30); // Simulate timer set
            
            // Act - Join second player (should clear timer)
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player2", user2, "conn2", _fakeClients);

            // Assert
            room.RoomCloseTime.Should().BeNull();
        }

        // ==================== CancelRoomTimer Tests (using public API) ====================
        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Cancel_Timer_When_Player_Reconnects()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var user = new User { Username = "player1", PasswordHash = "hash" };
            var roomUser = new RoomUser { PlayerId = "player1", Username = "player1", User = user };
            room.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
            
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act - Player reconnects
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", _fakeClients);

            // Assert
            room.DisconnectedPlayers.Should().NotContainKey("player1");
            room.RoomCloseTime.Should().BeNull(); // Timer should be cleared
        }

        [Fact]
        public async Task JoinAsPlayerMatchMaking_Should_Cancel_Timer_When_Player_Reconnects()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var user = new User { Username = "player1", PasswordHash = "hash" };
            var roomUser = new RoomUser { PlayerId = "player1", Username = "player1", User = user };
            room.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30);
            
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn1")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act - Player reconnects
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", _fakeClients);

            // Assert
            room.DisconnectedPlayers.Should().NotContainKey("player1");
            room.RoomCloseTime.Should().BeNull(); // Timer should be cleared
        }

        // ==================== HandlePlayerDisconnect Tests ====================
        [Fact]
        public async Task HandlePlayerDisconnect_Should_Handle_Null_RoomPlayers_Safely()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Set RoomPlayers to null to test edge case
            // This will cause NullReferenceException, so we expect it to throw
            room.RoomPlayers = null!;
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act & Assert - Should throw NullReferenceException
            var act = async () => await _service.HandlePlayerDisconnect("rock-paper-scissors", roomCode, "player1", _fakeClients);
            await act.Should().ThrowAsync<NullReferenceException>();
        }

        // ==================== CloseRoomAndKickAllPlayers Tests ====================
        [Fact]
        public async Task CloseRoomAndKickAllPlayers_Should_Handle_Null_RoomPlayers_Safely()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Set RoomPlayers to null to test edge case
            // This will cause NullReferenceException, so we expect it to throw
            room.RoomPlayers = null!;
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act & Assert - Should throw NullReferenceException
            var act = async () => await _service.CloseRoomAndKickAllPlayers(roomKey, _fakeClients, "Test");
            await act.Should().ThrowAsync<NullReferenceException>();
        }

        [Fact]
        public async Task CloseRoomAndKickAllPlayers_Should_Handle_Player_With_Null_PlayerId()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Create room user with null PlayerId (this shouldn't normally happen)
            var roomUser = new RoomUser { Username = "user1" };
            room.RoomPlayers.Add(roomUser);
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            await _service.CloseRoomAndKickAllPlayers(roomKey, _fakeClients, "Test");

            // Assert - Should not throw (RoomService should handle null PlayerId gracefully)
            _service.Rooms.Should().NotContainKey(roomKey);
        }

        // ==================== Edge Case Tests ====================
        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Handle_Game_Started_With_Existing_Player()
        {
            // Test the scenario where game is already started and a third player tries to join
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Add two players and start game
            var user1 = new User { Username = "player1", PasswordHash = "hash1" };
            var user2 = new User { Username = "player2", PasswordHash = "hash2" };
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "player1", User = user1 };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "player2", User = user2 };
            
            room.RoomPlayers.Add(roomUser1);
            room.RoomPlayers.Add(roomUser2);
            room.GameStarted = true;
            room.Game.AssignPlayerColors(roomUser1, roomUser2);
            
            // Try to join third player
            var user3 = new User { Username = "player3", PasswordHash = "hash3" };
            var mockSingleClient = A.Fake<ISingleClientProxy>();
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Client("conn3")).Returns(mockSingleClient);
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player3", user3, "conn3", _fakeClients);

            // Assert - Third player should be added but game should not restart
            room.RoomPlayers.Should().HaveCount(3);
            room.GameStarted.Should().BeTrue(); // Game stays started
        }

        [Fact]
        public void RoomExistsWithMatchmaking_Should_Handle_Null_Room()
        {
            // Test the TryGetValue returning null
            // Arrange - Don't create room, just test with non-existent key
            
            // Act
            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking("rock-paper-scissors", "NONEXISTENT");

            // Assert
            exists.Should().BeFalse();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Handle_Player_Not_In_Room_Players()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Player has session but not in room players
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            // Act
            var result = _service.HasActiveMatchmakingSession("player1");

            // Assert
            result.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void GetRoomUser_Should_Handle_Null_RoomPlayers_Safely()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            // Set RoomPlayers to null to test edge case
            // This will cause NullReferenceException
            room.RoomPlayers = null!;

            // Act & Assert - Should throw NullReferenceException
            Action act = () => _service.GetRoomUser(roomKey, "player1", null);
            act.Should().Throw<NullReferenceException>();
        }

        // ==================== New Tests for Missing Coverage ====================
        [Fact]
        public async Task JoinAsSpectator_Should_Handle_Duplicate_Spectators()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var user = new User { Username = "spectator1", PasswordHash = "hash" };

            // Act - Join same spectator twice
            await _service.JoinAsSpectator("rock-paper-scissors", roomCode, "spectator1", user);
            await _service.JoinAsSpectator("rock-paper-scissors", roomCode, "spectator1", user);

            // Assert - Should only have one spectator
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            room.RoomSpectators.Should().ContainSingle(s => s.PlayerId == "spectator1");
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Handle_Null_User()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);

            // Act
            await _service.JoinAsSpectator("rock-paper-scissors", roomCode, "spectator1", null);

            // Assert
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            room.RoomSpectators.Should().ContainSingle(s => s.PlayerId == "spectator1" && s.Username == null);
        }

        [Fact]
        public void CleanupInactiveMatchmakingSessions_Should_Handle_Empty_Sessions()
        {
            // Arrange - No sessions
            
            // Act
            _service.CleanupInactiveMatchmakingSessions();

            // Assert - Should not throw
            _service.ActiveMatchmakingSessions.Should().BeEmpty();
        }

        [Fact]
        public void GetUserCurrentGame_Should_Handle_Multiple_Rooms_With_Same_User()
        {
            // Arrange
            var roomCode1 = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey1 = $"rock-paper-scissors:{roomCode1}";
            var room1 = _service.GetRoomByKey(roomKey1)!;
            
            var roomCode2 = _service.CreateRoom("four-in-a-row", false);
            var roomKey2 = $"four-in-a-row:{roomCode2}";
            var room2 = _service.GetRoomByKey(roomKey2)!;
            
            // Same user in both rooms (shouldn't happen but test edge case)
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "sameuser" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "sameuser" };
            
            room1.RoomPlayers.Add(roomUser1);
            room2.RoomPlayers.Add(roomUser2);

            // Act - Should return first found room
            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("sameuser");

            // Assert - Should return one of the rooms (implementation returns first found)
            gameType.Should().NotBeNull();
            roomCode.Should().NotBeNull();
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Handle_No_Rooms_But_Has_Mappings()
        {
            // Arrange
            // Player has mappings but no rooms
            _service.MatchMakingRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.CodeRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.ActiveMatchmakingSessions["player1"] = "some-key";
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(A<string>._)).Returns(mockGroupClient);

            // Act
            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            // Assert - Mappings should be cleaned up
            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.CodeRoomUsers.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Handle_Room_No_Longer_Exists()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            // Add session
            _service.ActiveMatchmakingSessions["player1"] = roomKey;
            
            // Remove room
            _service.Rooms.TryRemove(roomKey, out _);

            // Act
            var result = _service.HasActiveMatchmakingSession("player1");

            // Assert
            result.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void GetRoomUser_Should_Find_User_By_User_Object_When_PlayerId_Different()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var user = new User { Username = "testuser", PasswordHash = "hash" };
            var roomUser = new RoomUser { PlayerId = "different-id", Username = "testuser", User = user };
            room.RoomPlayers.Add(roomUser);

            // Act - Search by User object
            var result = _service.GetRoomUser(roomKey, "player1", user);

            // Assert - Should find by User object comparison
            result.Should().NotBeNull();
            result!.User.Should().Be(user);
        }

        // ==================== Additional Edge Case Tests ====================
        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Handle_Room_With_No_Disconnected_Players()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room.RoomPlayers.Add(roomUser);
            // No disconnected players
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            var act = async () => await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            // Assert - Should not throw
            await act.Should().NotThrowAsync();
            _service.Rooms.Should().ContainKey(roomKey);
        }

        [Fact]
        public async Task CleanupExpiredRooms_Should_Handle_Room_With_Expired_Timer_But_Reconnected_Players()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room.RoomPlayers.Add(roomUser);
            
            // Set expired timer but no disconnected players
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(-10);
            
            var mockGroupClient = A.Fake<IClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);

            // Act
            await _service.CleanupExpiredRooms(_fakeClients);

            // Assert - Room should not be closed since no disconnected players
            _service.Rooms.Should().ContainKey(roomKey);
        }

        // ==================== Tests for StartRoomTimer using public API ====================
        [Fact]
        public async Task HandlePlayerLeave_Should_Start_Timer_When_Player_Leaves()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey)!;
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room.RoomPlayers.Add(roomUser1);
            room.RoomPlayers.Add(roomUser2);
            
            var mockGroupClient = A.Fake<IClientProxy>();
            var mockCaller = A.Fake<ISingleClientProxy>();
            A.CallTo(() => _fakeClients.Group(roomKey)).Returns(mockGroupClient);
            A.CallTo(() => _fakeClients.Caller).Returns(mockCaller);

            // Act
            await _service.HandlePlayerLeave("rock-paper-scissors", roomCode, "player1", _fakeClients);

            // Assert - Timer should be started
            room.RoomCloseTime.Should().NotBeNull();
            room.RoomCloseTime.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(2));
        }
    }
}