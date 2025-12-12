using Xunit;
using FluentAssertions;
using Services;
using Models.InMemoryModels;
using Models;
using Games;

namespace backend.Tests
{
    public class RoomServiceQuickTests
    {
        private readonly RoomService _service;

        public RoomServiceQuickTests()
        {
            // Pass null for hub context since we're not testing SignalR
            _service = new RoomService(null!);
        }

        [Fact]
        public void CreateRoom_Should_Create_Room_And_Return_Code()
        {
            // Act
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);

            // Assert
            roomCode.Should().NotBeNullOrEmpty();
            roomCode.Should().HaveLength(6);
            
            var room = _service.GetRoomByKey($"rock-paper-scissors:{roomCode}");
            room.Should().NotBeNull();
            room!.IsMatchMaking.Should().BeFalse();
            room.GameStarted.Should().BeFalse();
        }

        [Fact]
        public void CreateRoom_Should_Generate_Unique_Codes()
        {
            // Act
            var code1 = _service.CreateRoom("rock-paper-scissors", false);
            var code2 = _service.CreateRoom("rock-paper-scissors", false);
            var code3 = _service.CreateRoom("rock-paper-scissors", false);

            // Assert
            code1.Should().NotBe(code2);
            code2.Should().NotBe(code3);
            code1.Should().NotBe(code3);
        }

        [Fact]
        public void GetRoomByKey_Should_Return_Null_When_Room_Does_Not_Exist()
        {
            // Act
            var room = _service.GetRoomByKey("nonexistent-key");

            // Assert
            room.Should().BeNull();
        }

        [Fact]
        public void GetRoomByKey_Should_Return_Room_When_Exists()
        {
            // Arrange
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";

            // Act
            var room = _service.GetRoomByKey(roomKey);

            // Assert
            room.Should().NotBeNull();
            room!.Game.Should().BeOfType<PairMatching>();
        }

        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public void RoomExists_Should_Return_True_When_Room_Exists(string gameType)
        {
            // Arrange
            var roomCode = _service.CreateRoom(gameType, false);

            // Act
            var exists = _service.RoomExists(gameType, roomCode);

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public void RoomExists_Should_Return_False_When_Room_Does_Not_Exist()
        {
            // Act
            var exists = _service.RoomExists("rock-paper-scissors", "NONEXISTENT");

            // Assert
            exists.Should().BeFalse();
        }

        [Theory]
        [InlineData("rock-paper-scissors", true)]
        [InlineData("four-in-a-row", true)]
        [InlineData("pair-matching", false)]
        public void RoomExistsWithMatchmaking_Should_Return_Correct_Values(string gameType, bool isMatchMaking)
        {
            // Arrange
            var roomCode = _service.CreateRoom(gameType, isMatchMaking);

            // Act
            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking(gameType, roomCode);

            // Assert
            exists.Should().BeTrue();
            isMatchmaking.Should().Be(isMatchMaking);
        }

        [Fact]
        public void RoomExistsWithMatchmaking_Should_Return_False_When_Room_Does_Not_Exist()
        {
            // Act
            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking("rock-paper-scissors", "NONEXISTENT");

            // Assert
            exists.Should().BeFalse();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_No_Session_Exists()
        {
            // Act
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            // Assert
            hasSession.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_True_When_Session_Exists()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            // Act
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            // Assert
            hasSession.Should().BeTrue();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_And_Cleanup_When_Room_Does_Not_Exist()
        {
            // Arrange
            _service.ActiveMatchmakingSessions["player1"] = "nonexistent-key";

            // Act
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            // Assert
            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void ClearActiveMatchmakingSession_Should_Remove_Session()
        {
            // Arrange
            _service.ActiveMatchmakingSessions["player1"] = "some-room-key";

            // Act
            _service.ClearActiveMatchmakingSession("player1");

            // Assert
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void CleanupInactiveMatchmakingSessions_Should_Remove_Sessions_For_Nonexistent_Rooms()
        {
            // Arrange
            _service.ActiveMatchmakingSessions["player1"] = "nonexistent-room";
            _service.ActiveMatchmakingSessions["player2"] = "nonexistent-room-2";

            // Act
            _service.CleanupInactiveMatchmakingSessions();

            // Assert
            _service.ActiveMatchmakingSessions.Should().BeEmpty();
        }

        [Fact]
        public void CleanupInactiveMatchmakingSessions_Should_Keep_Active_Sessions()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            _service.ActiveMatchmakingSessions["player1"] = roomKey;
            _service.ActiveMatchmakingSessions["player2"] = "nonexistent-room";

            // Act
            _service.CleanupInactiveMatchmakingSessions();

            // Assert
            _service.ActiveMatchmakingSessions.Should().ContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player2");
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Null_When_User_Not_In_Any_Game()
        {
            // Act
            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("testUser");

            // Assert
            gameType.Should().BeNull();
            roomCode.Should().BeNull();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Game_Info_When_User_In_Game_By_PlayerId()
        {
            // Arrange
            var code = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{code}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            // Act
            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("player1");

            // Assert
            gameType.Should().Be("rock-paper-scissors");
            roomCode.Should().Be(code);
            isMatchmaking.Should().BeTrue();
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Game_Info_When_User_In_Game_By_Username()
        {
            // Arrange
            var code = _service.CreateRoom("four-in-a-row", false);
            var roomKey = $"four-in-a-row:{code}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            // Act
            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("testUser");

            // Assert
            gameType.Should().Be("four-in-a-row");
            roomCode.Should().Be(code);
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Add_Spectator_To_Room()
        {
            // Arrange
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";
            var user = new User { Username = "spectator1", PasswordHash = "hash" };

            // Act
            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);

            // Assert
            var room = _service.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle();
            room.RoomSpectators[0].PlayerId.Should().Be("spectator1");
            room.RoomSpectators[0].Username.Should().Be("spectator1");
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Not_Add_Duplicate_Spectators()
        {
            // Arrange
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";
            var user = new User { Username = "spectator1", PasswordHash = "hash" };

            // Act
            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);
            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);

            // Assert
            var room = _service.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle();
        }

        [Fact]
        public void GetRoomUser_Should_Return_Null_When_Room_Does_Not_Exist()
        {
            // Act
            var roomUser = _service.GetRoomUser("nonexistent-key", "player1", null);

            // Assert
            roomUser.Should().BeNull();
        }

        [Fact]
        public void GetRoomUser_Should_Return_User_When_Found_By_PlayerId()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            // Act
            var result = _service.GetRoomUser(roomKey, "player1", null);

            // Assert
            result.Should().NotBeNull();
            result!.PlayerId.Should().Be("player1");
        }

        [Fact]
        public void GetRoomUser_Should_Return_User_When_Found_By_User_Object()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var user = new User { Username = "testUser", PasswordHash = "hash" };
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser", User = user };
            room!.RoomPlayers.Add(roomUser);

            // Act
            var result = _service.GetRoomUser(roomKey, "player1", user);

            // Assert
            result.Should().NotBeNull();
            result!.User.Should().Be(user);
        }

        [Fact]
        public void GetRoomUser_Should_Return_Null_When_Player_Not_In_Room()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            // Act
            var result = _service.GetRoomUser(roomKey, "nonexistent", null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Rooms_Should_Be_ConcurrentDictionary()
        {
            // Assert
            _service.Rooms.Should().NotBeNull();
            _service.Rooms.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, Room>>();
        }

        [Fact]
        public void CodeRoomUsers_Should_Be_ConcurrentDictionary()
        {
            // Assert
            _service.CodeRoomUsers.Should().NotBeNull();
            _service.CodeRoomUsers.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, RoomUser>>();
        }

        [Fact]
        public void MatchMakingRoomUsers_Should_Be_ConcurrentDictionary()
        {
            // Assert
            _service.MatchMakingRoomUsers.Should().NotBeNull();
            _service.MatchMakingRoomUsers.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, RoomUser>>();
        }

        [Fact]
        public void ActiveMatchmakingSessions_Should_Be_ConcurrentDictionary()
        {
            // Assert
            _service.ActiveMatchmakingSessions.Should().NotBeNull();
            _service.ActiveMatchmakingSessions.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, string>>();
        }

        [Fact]
        public void GenerateRoomCode_Should_Generate_Unique_Codes()
        {
            // Act - Generate multiple codes
            var codes = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                var code = _service.CreateRoom("rock-paper-scissors", false);
                codes.Add(code);
            }
            
            // Assert - All codes should be unique
            codes.Should().HaveCount(10);
        }

        [Fact]
        public void CreateRoom_Should_Create_Different_Game_Types()
        {
            // Act
            var rpsCode = _service.CreateRoom("rock-paper-scissors", false);
            var fourCode = _service.CreateRoom("four-in-a-row", false);
            var pairCode = _service.CreateRoom("pair-matching", false);

            // Assert
            var rpsRoom = _service.GetRoomByKey($"rock-paper-scissors:{rpsCode}");
            var fourRoom = _service.GetRoomByKey($"four-in-a-row:{fourCode}");
            var pairRoom = _service.GetRoomByKey($"pair-matching:{pairCode}");

            rpsRoom!.Game.Should().BeOfType<RockPaperScissors>();
            fourRoom!.Game.Should().BeOfType<FourInARowGame>();
            pairRoom!.Game.Should().BeOfType<PairMatching>();
        }

        [Fact]
        public void RoomExistsWithMatchmaking_Should_Return_Correct_IsMatchmaking_False()
        {
            // Arrange
            var roomCode = _service.CreateRoom("pair-matching", false);

            // Act
            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking("pair-matching", roomCode);

            // Assert
            exists.Should().BeTrue();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_Player_Is_Disconnected()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            // Act
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            // Assert
            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_Player_Not_In_Room()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            // Act
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            // Assert
            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        // Test basic JoinAsPlayer functionality without SignalR verification
        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Add_Player_To_Room()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var user = new User { Username = "player1", PasswordHash = "hash" };

            // Use a simple mock for IHubCallerClients
            var mockClients = new MockClients();

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", mockClients);

            // Assert
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            room!.RoomPlayers.Should().ContainSingle(p => p.PlayerId == "player1");
        }

        [Fact]
        public async Task JoinAsPlayerMatchMaking_Should_Add_Player_To_Room()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var user = new User { Username = "player1", PasswordHash = "hash" };

            // Use a simple mock for IHubCallerClients
            var mockClients = new MockClients();

            // Act
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player1", user, "conn1", mockClients);

            // Assert
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            room!.RoomPlayers.Should().ContainSingle(p => p.PlayerId == "player1");
            _service.ActiveMatchmakingSessions.Should().ContainKey("player1");
        }

        [Fact]
        public async Task JoinAsPlayerNotMatchMaking_Should_Start_Game_When_Two_Players_Join()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var user1 = new User { Username = "player1", PasswordHash = "hash1" };
            var user2 = new User { Username = "player2", PasswordHash = "hash2" };

            // Use a simple mock for IHubCallerClients
            var mockClients = new MockClients();

            // Act
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player1", user1, "conn1", mockClients);
            await _service.JoinAsPlayerNotMatchMaking("rock-paper-scissors", roomCode, "player2", user2, "conn2", mockClients);

            // Assert
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            room!.GameStarted.Should().BeTrue();
            room.RoomPlayers.Should().HaveCount(2);
        }

        [Fact]
        public async Task JoinAsPlayerMatchMaking_Should_Start_Game_When_Two_Players_Join()
        {
            // Arrange
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var user1 = new User { Username = "player1", PasswordHash = "hash1" };
            var user2 = new User { Username = "player2", PasswordHash = "hash2" };

            // Use a simple mock for IHubCallerClients
            var mockClients = new MockClients();

            // Act
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player1", user1, "conn1", mockClients);
            await _service.JoinAsPlayerMatchMaking("rock-paper-scissors", roomCode, "player2", user2, "conn2", mockClients);

            // Assert
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            room!.GameStarted.Should().BeTrue();
            room.RoomPlayers.Should().HaveCount(2);
        }

        // Test for cleanup when player is not in any room
        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Clean_Mappings_When_No_Rooms()
        {
            // Arrange
            _service.MatchMakingRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.CodeRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.ActiveMatchmakingSessions["player1"] = "some-key";

            // Use a simple mock for IHubCallerClients
            var mockClients = new MockClients();

            // Act
            await _service.ForceRemovePlayerFromAllRooms("player1", mockClients);

            // Assert
            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.CodeRoomUsers.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        // Simple mock implementation for IHubCallerClients
        private class MockClients : Microsoft.AspNetCore.SignalR.IHubCallerClients
        {
            public Microsoft.AspNetCore.SignalR.IClientProxy Caller => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Others => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy All => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> connectionIds) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> groupNames) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy OthersInGroup(string groupName) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => new MockClientProxy();
            public Microsoft.AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> userIds) => new MockClientProxy();
        }

        private class MockClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                // Do nothing - just return completed task
                return Task.CompletedTask;
            }
        }
    }
}