using Xunit;
using FluentAssertions;
using Services;
using Microsoft.AspNetCore.SignalR;
using FakeItEasy;
using Hubs;
using Models.InMemoryModels;
using Models;
using Games;

namespace backend.Tests
{
    public class RoomServiceTests
    {
        private readonly RoomService _service;
        private readonly IHubContext<SpectatorHub> _hubContext;
        private readonly IHubCallerClients _fakeClients;

        public RoomServiceTests()
        {
            _hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _service = new RoomService(_hubContext);
            _fakeClients = A.Fake<IHubCallerClients>();
        }

        [Theory]
        [InlineData("rock-paper-scissors", false)]
        [InlineData("four-in-a-row", false)]
        [InlineData("pair-matching", false)]
        [InlineData("rock-paper-scissors", true)]
        [InlineData("four-in-a-row", true)]
        [InlineData("pair-matching", true)]
        public void CreateRoom_Should_Create_Room_And_Return_Code(string gameType, bool isMatchMaking)
        {
            var roomCode = _service.CreateRoom(gameType, isMatchMaking);

            roomCode.Should().NotBeNullOrEmpty();
            roomCode.Should().HaveLength(6);
            
            var room = _service.GetRoomByKey($"{gameType}:{roomCode}");
            room.Should().NotBeNull();
            room!.IsMatchMaking.Should().Be(isMatchMaking);
            room.GameStarted.Should().BeFalse();
            room.RoomPlayers.Should().BeEmpty();
        }

        [Fact]
        public void CreateRoom_Should_Generate_Unique_Codes()
        {
            var code1 = _service.CreateRoom("rock-paper-scissors", false);
            var code2 = _service.CreateRoom("rock-paper-scissors", false);
            var code3 = _service.CreateRoom("rock-paper-scissors", false);

            code1.Should().NotBe(code2);
            code2.Should().NotBe(code3);
            code1.Should().NotBe(code3);
        }

        [Fact]
        public void GetRoomByKey_Should_Return_Null_When_Room_Does_Not_Exist()
        {
            var room = _service.GetRoomByKey("nonexistent-key");

            room.Should().BeNull();
        }

        [Fact]
        public void GetRoomByKey_Should_Return_Room_When_Exists()
        {
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";

            var room = _service.GetRoomByKey(roomKey);

            room.Should().NotBeNull();
            room!.Game.Should().BeOfType<PairMatching>();
        }

        [Theory]
        [InlineData("rock-paper-scissors")]
        [InlineData("four-in-a-row")]
        [InlineData("pair-matching")]
        public void RoomExists_Should_Return_True_When_Room_Exists(string gameType)
        {
            var roomCode = _service.CreateRoom(gameType, false);

            var exists = _service.RoomExists(gameType, roomCode);

            exists.Should().BeTrue();
        }

        [Fact]
        public void RoomExists_Should_Return_False_When_Room_Does_Not_Exist()
        {
            var exists = _service.RoomExists("rock-paper-scissors", "NONEXISTENT");

            exists.Should().BeFalse();
        }

        [Theory]
        [InlineData("rock-paper-scissors", true)]
        [InlineData("four-in-a-row", true)]
        [InlineData("pair-matching", false)]
        public void RoomExistsWithMatchmaking_Should_Return_Correct_Values(string gameType, bool isMatchMaking)
        {
            var roomCode = _service.CreateRoom(gameType, isMatchMaking);

            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking(gameType, roomCode);

            exists.Should().BeTrue();
            isMatchmaking.Should().Be(isMatchMaking);
        }

        [Fact]
        public void RoomExistsWithMatchmaking_Should_Return_False_When_Room_Does_Not_Exist()
        {
            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking("rock-paper-scissors", "NONEXISTENT");

            exists.Should().BeFalse();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_No_Session_Exists()
        {
            var hasSession = _service.HasActiveMatchmakingSession("player1");

            hasSession.Should().BeFalse();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_True_When_Session_Exists()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            var hasSession = _service.HasActiveMatchmakingSession("player1");

            hasSession.Should().BeTrue();
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_And_Cleanup_When_Room_Does_Not_Exist()
        {
            _service.ActiveMatchmakingSessions["player1"] = "nonexistent-key";

            var hasSession = _service.HasActiveMatchmakingSession("player1");

            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_Player_Is_Disconnected()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            var hasSession = _service.HasActiveMatchmakingSession("player1");

            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void HasActiveMatchmakingSession_Should_Return_False_When_Player_Not_In_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            var hasSession = _service.HasActiveMatchmakingSession("player1");

            hasSession.Should().BeFalse();
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void ClearActiveMatchmakingSession_Should_Remove_Session()
        {
            _service.ActiveMatchmakingSessions["player1"] = "some-room-key";

            _service.ClearActiveMatchmakingSession("player1");

            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public void CleanupInactiveMatchmakingSessions_Should_Remove_Sessions_For_Nonexistent_Rooms()
        {
            _service.ActiveMatchmakingSessions["player1"] = "nonexistent-room";
            _service.ActiveMatchmakingSessions["player2"] = "nonexistent-room-2";

            _service.CleanupInactiveMatchmakingSessions();

            _service.ActiveMatchmakingSessions.Should().BeEmpty();
        }

        [Fact]
        public void CleanupInactiveMatchmakingSessions_Should_Keep_Active_Sessions()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);
            _service.ActiveMatchmakingSessions["player1"] = roomKey;
            _service.ActiveMatchmakingSessions["player2"] = "nonexistent-room";

            _service.CleanupInactiveMatchmakingSessions();

            _service.ActiveMatchmakingSessions.Should().ContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player2");
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Null_When_User_Not_In_Any_Game()
        {
            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("testUser");

            gameType.Should().BeNull();
            roomCode.Should().BeNull();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Game_Info_When_User_In_Game_By_PlayerId()
        {
            var code = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{code}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("player1");

            gameType.Should().Be("rock-paper-scissors");
            roomCode.Should().Be(code);
            isMatchmaking.Should().BeTrue();
        }

        [Fact]
        public void GetUserCurrentGame_Should_Return_Game_Info_When_User_In_Game_By_Username()
        {
            var code = _service.CreateRoom("four-in-a-row", false);
            var roomKey = $"four-in-a-row:{code}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            var (gameType, roomCode, isMatchmaking) = _service.GetUserCurrentGame("testUser");

            gameType.Should().Be("four-in-a-row");
            roomCode.Should().Be(code);
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Add_Spectator_To_Room()
        {
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";
            var user = new User { Username = "spectator1", PasswordHash = "hash" };

            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);

            var room = _service.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle();
            room.RoomSpectators[0].PlayerId.Should().Be("spectator1");
            room.RoomSpectators[0].Username.Should().Be("spectator1");
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Not_Add_Duplicate_Spectators()
        {
            var roomCode = _service.CreateRoom("pair-matching", false);
            var roomKey = $"pair-matching:{roomCode}";
            var user = new User { Username = "spectator1", PasswordHash = "hash" };

            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);
            await _service.JoinAsSpectator("pair-matching", roomCode, "spectator1", user);

            var room = _service.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle();
        }

        [Fact]
        public async Task JoinAsSpectator_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _service.JoinAsSpectator("pair-matching", "NONEXISTENT", "spectator1");
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void GetRoomUser_Should_Return_Null_When_Room_Does_Not_Exist()
        {
            var roomUser = _service.GetRoomUser("nonexistent-key", "player1", null);

            roomUser.Should().BeNull();
        }

        [Fact]
        public void GetRoomUser_Should_Return_User_When_Found_By_PlayerId()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser" };
            room!.RoomPlayers.Add(roomUser);

            var result = _service.GetRoomUser(roomKey, "player1", null);

            result.Should().NotBeNull();
            result!.PlayerId.Should().Be("player1");
        }

        [Fact]
        public void GetRoomUser_Should_Return_User_When_Found_By_User_Object()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var user = new User { Username = "testUser", PasswordHash = "hash" };
            var roomUser = new RoomUser { PlayerId = "player1", Username = "testUser", User = user };
            room!.RoomPlayers.Add(roomUser);

            var result = _service.GetRoomUser(roomKey, "player1", user);

            result.Should().NotBeNull();
            result!.User.Should().Be(user);
        }

        [Fact]
        public async Task HandlePlayerDisconnect_Should_Add_Player_To_Disconnected_List()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);

            await _service.HandlePlayerDisconnect("rock-paper-scissors", roomCode, "player1", _fakeClients);

            room.DisconnectedPlayers.Should().ContainKey("player1");
            room.RoomCloseTime.Should().NotBeNull();
        }

        [Fact]
        public async Task HandlePlayerDisconnect_Should_Close_Room_When_All_Players_Disconnect()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);

            await _service.HandlePlayerDisconnect("rock-paper-scissors", roomCode, "player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task HandlePlayerDisconnect_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _service.HandlePlayerDisconnect("rock-paper-scissors", "NONEXISTENT", "player1", _fakeClients);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task HandlePlayerLeave_Should_Remove_Player_From_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);

            await _service.HandlePlayerLeave("rock-paper-scissors", roomCode, "player1", _fakeClients);

            room.RoomPlayers.Should().NotContain(rp => rp.PlayerId == "player1");
            room.RoomPlayers.Should().Contain(rp => rp.PlayerId == "player2");
        }

        [Fact]
        public async Task HandlePlayerLeave_Should_Close_Room_When_Last_Player_Leaves()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);

            await _service.HandlePlayerLeave("rock-paper-scissors", roomCode, "player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task HandlePlayerLeave_Should_Close_Matchmaking_Room_Immediately()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);
            _service.ActiveMatchmakingSessions["player1"] = roomKey;
            _service.ActiveMatchmakingSessions["player2"] = roomKey;

            await _service.HandlePlayerLeave("rock-paper-scissors", roomCode, "player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player2");
        }

        [Fact]
        public async Task HandlePlayerLeave_Should_Not_Throw_When_Player_Not_In_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);

            var act = async () => await _service.HandlePlayerLeave("rock-paper-scissors", roomCode, "nonexistent", _fakeClients);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task CloseRoomAndKickAllPlayers_Should_Remove_Room_And_Clean_Mappings()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            _service.CodeRoomUsers["player1"] = roomUser;

            await _service.CloseRoomAndKickAllPlayers(roomKey, _fakeClients, "Test closure");

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.CodeRoomUsers.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task CloseRoomAndKickAllPlayers_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _service.CloseRoomAndKickAllPlayers("nonexistent-key", _fakeClients, "Test");
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task CloseRoomAndKickAllPlayers_Should_Exclude_Specified_Player()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);
            _service.MatchMakingRoomUsers["player1"] = roomUser1;
            _service.MatchMakingRoomUsers["player2"] = roomUser2;
            _service.ActiveMatchmakingSessions["player1"] = roomKey;
            _service.ActiveMatchmakingSessions["player2"] = roomKey;

            await _service.CloseRoomAndKickAllPlayers(roomKey, _fakeClients, "Test", excludePlayerId: "player1");

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.MatchMakingRoomUsers.Should().NotContainKey("player2");
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Remove_Player_From_All_Rooms()
        {
            var roomCode1 = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey1 = $"rock-paper-scissors:{roomCode1}";
            var room1 = _service.GetRoomByKey(roomKey1);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room1!.RoomPlayers.Add(roomUser);
            _service.CodeRoomUsers["player1"] = roomUser;

            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey1);
            _service.CodeRoomUsers.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Clean_Mappings_When_No_Rooms()
        {
            _service.MatchMakingRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.CodeRoomUsers["player1"] = new RoomUser { PlayerId = "player1" };
            _service.ActiveMatchmakingSessions["player1"] = "some-key";

            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.CodeRoomUsers.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Close_Matchmaking_Rooms()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);
            _service.MatchMakingRoomUsers["player1"] = roomUser1;
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Close_Empty_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Close_Room_When_Timer_Expired()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1); // Already expired

            await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Not_Close_Room_When_Timer_Not_Expired()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30); // Not expired

            await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            _service.Rooms.Should().ContainKey(roomKey);
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _service.CheckAndCloseRoomIfNeeded("nonexistent-key", _fakeClients);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task CleanupExpiredRooms_Should_Remove_Expired_Rooms()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1); // Already expired

            await _service.CleanupExpiredRooms(_fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
        }

        [Fact]
        public async Task CleanupExpiredRooms_Should_Not_Remove_Non_Expired_Rooms()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(30); // Not expired
            await _service.CleanupExpiredRooms(_fakeClients);

            _service.Rooms.Should().ContainKey(roomKey);
        }

        [Fact]
        public void Rooms_Should_Be_ConcurrentDictionary()
        {
            _service.Rooms.Should().NotBeNull();
            _service.Rooms.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, Room>>();
        }

        [Fact]
        public void CodeRoomUsers_Should_Be_ConcurrentDictionary()
        {
            _service.CodeRoomUsers.Should().NotBeNull();
            _service.CodeRoomUsers.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, RoomUser>>();
        }

        [Fact]
        public void MatchMakingRoomUsers_Should_Be_ConcurrentDictionary()
        {
            _service.MatchMakingRoomUsers.Should().NotBeNull();
            _service.MatchMakingRoomUsers.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, RoomUser>>();
        }

        [Fact]
        public void ActiveMatchmakingSessions_Should_Be_ConcurrentDictionary()
        {
            _service.ActiveMatchmakingSessions.Should().NotBeNull();
            _service.ActiveMatchmakingSessions.Should().BeAssignableTo<System.Collections.Concurrent.ConcurrentDictionary<string, string>>();
        }

        [Fact]
        public async Task ReportWin_Should_Call_Game_ReportWin_When_Player_In_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "winner" };
            room!.RoomPlayers.Add(roomUser);
            room.GameStarted = true;

            await _service.ReportWin("player1", _fakeClients);

            room.Game.Should().NotBeNull();
        }

        [Fact]
        public async Task ReportWin_Should_Not_Throw_When_Player_Not_In_Any_Room()
        {
            var act = async () => await _service.ReportWin("nonexistent", _fakeClients);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void StartRoomTimer_Should_Set_RoomCloseTime_And_Cancellation_Token()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);

            _service.StartRoomTimer(roomKey, room, _fakeClients, "Test reason");

            room.RoomCloseTime.Should().NotBeNull();
            room.RoomCloseTime.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(2));
            room.RoomTimerCancellation.Should().NotBeNull();
        }

        [Fact]
        public void StartRoomTimer_Should_Cancel_Existing_Timer_Before_Starting_New_One()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);

            _service.StartRoomTimer(roomKey, room, _fakeClients, "First timer");
            var firstCts = room.RoomTimerCancellation;

            _service.StartRoomTimer(roomKey, room, _fakeClients, "Second timer");
            var secondCts = room.RoomTimerCancellation;

            firstCts.Should().NotBeSameAs(secondCts);
            room.RoomTimerCancellation.Should().NotBeNull();
        }

        [Fact]
        public void GetRoomUser_Should_Return_Null_When_Player_Not_In_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            var result = _service.GetRoomUser(roomKey, "nonexistent", null);

            result.Should().BeNull();
        }

        [Fact]
        public async Task HandlePlayerDisconnect_Should_Not_Throw_When_Player_Not_In_Room()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            
            var act = async () => await _service.HandlePlayerDisconnect("rock-paper-scissors", roomCode, "nonexistent", _fakeClients);
            
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Handle_Player_In_Disconnected_List()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.DisconnectedPlayers["player1"] = roomUser;
            _service.CodeRoomUsers["player1"] = roomUser;

            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            _service.CodeRoomUsers.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task ForceRemovePlayerFromAllRooms_Should_Close_NonMatchmaking_Room_With_Other_Players()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser1 = new RoomUser { PlayerId = "player1", Username = "user1" };
            var roomUser2 = new RoomUser { PlayerId = "player2", Username = "user2" };
            room!.RoomPlayers.Add(roomUser1);
            room!.RoomPlayers.Add(roomUser2);
            _service.CodeRoomUsers["player1"] = roomUser1;

            await _service.ForceRemovePlayerFromAllRooms("player1", _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.CodeRoomUsers.Should().NotContainKey("player1");
        }

        [Fact]
        public void RoomExistsWithMatchmaking_Should_Return_Correct_IsMatchmaking_False()
        {
            var roomCode = _service.CreateRoom("pair-matching", false);

            var (exists, isMatchmaking) = _service.RoomExistsWithMatchmaking("pair-matching", roomCode);

            exists.Should().BeTrue();
            isMatchmaking.Should().BeFalse();
        }

        [Fact]
        public async Task CleanupExpiredRooms_Should_Handle_Multiple_Expired_Rooms()
        {
            var roomCode1 = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey1 = $"rock-paper-scissors:{roomCode1}";
            var room1 = _service.GetRoomByKey(roomKey1);
            room1!.RoomPlayers.Add(new RoomUser { PlayerId = "player1", Username = "user1" });
            room1.DisconnectedPlayers["player1"] = room1.RoomPlayers[0];
            room1.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1);

            var roomCode2 = _service.CreateRoom("four-in-a-row", false);
            var roomKey2 = $"four-in-a-row:{roomCode2}";
            var room2 = _service.GetRoomByKey(roomKey2);
            room2!.RoomPlayers.Add(new RoomUser { PlayerId = "player2", Username = "user2" });
            room2.DisconnectedPlayers["player2"] = room2.RoomPlayers[0];
            room2.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1);

            await _service.CleanupExpiredRooms(_fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey1);
            _service.Rooms.Should().NotContainKey(roomKey2);
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Clean_Player_Mappings_For_Matchmaking()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", true);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1);
            _service.MatchMakingRoomUsers["player1"] = roomUser;
            _service.ActiveMatchmakingSessions["player1"] = roomKey;

            await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.MatchMakingRoomUsers.Should().NotContainKey("player1");
            _service.ActiveMatchmakingSessions.Should().NotContainKey("player1");
        }

        [Fact]
        public async Task CheckAndCloseRoomIfNeeded_Should_Clean_Player_Mappings_For_CodeRoom()
        {
            var roomCode = _service.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _service.GetRoomByKey(roomKey);
            
            var roomUser = new RoomUser { PlayerId = "player1", Username = "user1" };
            room!.RoomPlayers.Add(roomUser);
            room.DisconnectedPlayers["player1"] = roomUser;
            room.RoomCloseTime = DateTime.UtcNow.AddSeconds(-1);
            _service.CodeRoomUsers["player1"] = roomUser;

            await _service.CheckAndCloseRoomIfNeeded(roomKey, _fakeClients);

            _service.Rooms.Should().NotContainKey(roomKey);
            _service.CodeRoomUsers.Should().NotContainKey("player1");
        }

        [Fact]
        public void CreateRoom_Should_Create_Different_Game_Types()
        {
            var rpsCode = _service.CreateRoom("rock-paper-scissors", false);
            var fourCode = _service.CreateRoom("four-in-a-row", false);
            var pairCode = _service.CreateRoom("pair-matching", false);

            var rpsRoom = _service.GetRoomByKey($"rock-paper-scissors:{rpsCode}");
            var fourRoom = _service.GetRoomByKey($"four-in-a-row:{fourCode}");
            var pairRoom = _service.GetRoomByKey($"pair-matching:{pairCode}");

            rpsRoom!.Game.Should().BeOfType<RockPaperScissors>();
            fourRoom!.Game.Should().BeOfType<FourInARowGame>();
            pairRoom!.Game.Should().BeOfType<PairMatching>();
        }
    }
}
