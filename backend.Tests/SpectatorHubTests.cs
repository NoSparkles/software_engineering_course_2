using Xunit;
using FluentAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.SignalR;
using Hubs;
using Services;
using Models.InMemoryModels;
using Models;
using Games;
using System.Threading;

namespace backend.Tests
{
    public class SpectatorHubTests
    {
        private readonly SpectatorHub _hub;
        private readonly RoomService _roomService;
        private readonly IHubCallerClients _fakeClients;
        private readonly ISingleClientProxy _fakeCallerProxy;
        private readonly IClientProxy _fakeGroupProxy;
        private readonly HubCallerContext _fakeContext;
        private readonly IGroupManager _fakeGroupManager;

        public SpectatorHubTests()
        {
            var hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);
            _hub = new SpectatorHub(_roomService);

            _fakeClients = A.Fake<IHubCallerClients>();
            _fakeCallerProxy = A.Fake<ISingleClientProxy>();
            _fakeGroupProxy = A.Fake<IClientProxy>();
            _fakeContext = A.Fake<HubCallerContext>();
            _fakeGroupManager = A.Fake<IGroupManager>();

            A.CallTo(() => _fakeClients.Caller).Returns(_fakeCallerProxy);
            A.CallTo(() => _fakeClients.Group(A<string>.Ignored)).Returns(_fakeGroupProxy);
            A.CallTo(() => _fakeContext.ConnectionId).Returns("test-connection-id");
            A.CallTo(() => _fakeGroupManager.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(Task.CompletedTask);
            A.CallTo(() => _fakeGroupManager.RemoveFromGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(Task.CompletedTask);

            _hub.Clients = _fakeClients;
            _hub.Context = _fakeContext;
            _hub.Groups = _fakeGroupManager;
        }

        [Fact]
        public async Task JoinSpectate_Should_Send_JoinFailed_When_Room_Does_Not_Exist()
        {
            await _hub.JoinSpectate("rock-paper-scissors", "NONEXISTENT", "spectator1", "John");

            A.CallTo(() => _fakeCallerProxy.SendCoreAsync("JoinFailed", A<object[]>.That.Matches(o => 
                o.Length > 0 && o[0].ToString() == "Room does not exist"), A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Add_Spectator_To_Room()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");

            var room = _roomService.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle(s => s.PlayerId == "spectator1");
        }

        [Fact]
        public async Task JoinSpectate_Should_Add_To_SignalR_Group()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");

            A.CallTo(() => _fakeGroupManager.AddToGroupAsync("test-connection-id", 
                $"rock-paper-scissors:{roomCode}", A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Send_GameStateUpdate_For_RockPaperScissors()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");

            A.CallTo(() => _fakeCallerProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Send_GameStateUpdate_For_FourInARow()
        {
            var roomCode = _roomService.CreateRoom("four-in-a-row", false);

            await _hub.JoinSpectate("four-in-a-row", roomCode, "spectator1", "John");

            A.CallTo(() => _fakeCallerProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Send_GameStateUpdate_For_PairMatching()
        {
            var roomCode = _roomService.CreateRoom("pair-matching", false);

            await _hub.JoinSpectate("pair-matching", roomCode, "spectator1", "John");

            A.CallTo(() => _fakeCallerProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Notify_Group_About_New_Spectator()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("SpectatorJoined", 
                A<object[]>.That.Matches(o => o.Length >= 2 && o[0].ToString() == "spectator1" && o[1].ToString() == "John"), 
                A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectate_Should_Handle_Null_Username()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", null);

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("SpectatorJoined", 
                A<object[]>.That.Matches(o => o.Length >= 2 && o[1].ToString() == ""), 
                A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectateByUsername_Should_Find_User_Game_And_Join()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            var room = _roomService.GetRoomByKey(roomKey);
            var player = new RoomUser { PlayerId = "player1", Username = "Alice" };
            room!.RoomPlayers.Add(player);

            await _hub.JoinSpectateByUsername("Alice", "spectator1", "Bob");

            A.CallTo(() => _fakeGroupManager.AddToGroupAsync("test-connection-id", roomKey, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task JoinSpectateByUsername_Should_Send_JoinFailed_When_User_Not_In_Game()
        {
            await _hub.JoinSpectateByUsername("NonExistentUser", "spectator1", "Bob");

            A.CallTo(() => _fakeCallerProxy.SendCoreAsync("JoinFailed", 
                A<object[]>.That.Matches(o => o.Length > 0 && o[0].ToString() == "User is not currently in a game"), 
                A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task LeaveSpectate_Should_Remove_Spectator_From_Room()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await _hub.LeaveSpectate("rock-paper-scissors", roomCode, "spectator1");

            var room = _roomService.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().BeEmpty();
        }

        [Fact]
        public async Task LeaveSpectate_Should_Remove_From_SignalR_Group()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            
            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await _hub.LeaveSpectate("rock-paper-scissors", roomCode, "spectator1");

            A.CallTo(() => _fakeGroupManager.RemoveFromGroupAsync("test-connection-id", 
                $"rock-paper-scissors:{roomCode}", A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task LeaveSpectate_Should_Notify_Group_About_Spectator_Leaving()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            
            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await _hub.LeaveSpectate("rock-paper-scissors", roomCode, "spectator1");

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("SpectatorLeft", 
                A<object[]>.That.Matches(o => o.Length > 0 && o[0].ToString() == "spectator1"), 
                A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task LeaveSpectate_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _hub.LeaveSpectate("rock-paper-scissors", "NONEXISTENT", "spectator1");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task OnDisconnectedAsync_Should_Remove_Spectator_From_Room()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";
            
            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await _hub.OnDisconnectedAsync(null);

            var room = _roomService.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().BeEmpty();
        }

        [Fact]
        public async Task OnDisconnectedAsync_Should_Notify_Group_About_Disconnect()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            
            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await _hub.OnDisconnectedAsync(null);

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("SpectatorLeft", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task OnDisconnectedAsync_Should_Not_Throw_When_No_Connection_Mapped()
        {
            var act = async () => await _hub.OnDisconnectedAsync(null);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task BroadcastToSpectators_Should_Send_GameStateUpdate_For_RockPaperScissors()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);

            await _hub.BroadcastToSpectators("rock-paper-scissors", roomCode);

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task BroadcastToSpectators_Should_Send_GameStateUpdate_For_FourInARow()
        {
            var roomCode = _roomService.CreateRoom("four-in-a-row", false);

            await _hub.BroadcastToSpectators("four-in-a-row", roomCode);

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task BroadcastToSpectators_Should_Send_GameStateUpdate_For_PairMatching()
        {
            var roomCode = _roomService.CreateRoom("pair-matching", false);

            await _hub.BroadcastToSpectators("pair-matching", roomCode);

            A.CallTo(() => _fakeGroupProxy.SendCoreAsync("GameStateUpdate", 
                A<object[]>._, A<CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task BroadcastToSpectators_Should_Not_Throw_When_Room_Does_Not_Exist()
        {
            var act = async () => await _hub.BroadcastToSpectators("rock-paper-scissors", "NONEXISTENT");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task JoinSpectate_Should_Handle_Multiple_Spectators()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            var hub1 = new SpectatorHub(_roomService)
            {
                Clients = _fakeClients,
                Context = _fakeContext,
                Groups = _fakeGroupManager
            };

            var fakeContext2 = A.Fake<HubCallerContext>();
            A.CallTo(() => fakeContext2.ConnectionId).Returns("connection-2");
            
            var hub2 = new SpectatorHub(_roomService)
            {
                Clients = _fakeClients,
                Context = fakeContext2,
                Groups = _fakeGroupManager
            };

            await hub1.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            await hub2.JoinSpectate("rock-paper-scissors", roomCode, "spectator2", "Jane");

            var room = _roomService.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().HaveCount(2);
        }

        [Fact]
        public async Task LeaveSpectate_Should_Not_Remove_Other_Spectators()
        {
            var roomCode = _roomService.CreateRoom("rock-paper-scissors", false);
            var roomKey = $"rock-paper-scissors:{roomCode}";

            await _hub.JoinSpectate("rock-paper-scissors", roomCode, "spectator1", "John");
            
            var hub2 = new SpectatorHub(_roomService)
            {
                Clients = _fakeClients,
                Context = A.Fake<HubCallerContext>(),
                Groups = _fakeGroupManager
            };
            await hub2.JoinSpectate("rock-paper-scissors", roomCode, "spectator2", "Jane");

            await _hub.LeaveSpectate("rock-paper-scissors", roomCode, "spectator1");

            var room = _roomService.GetRoomByKey(roomKey);
            room!.RoomSpectators.Should().ContainSingle();
            room.RoomSpectators[0].PlayerId.Should().Be("spectator2");
        }
    }
}
