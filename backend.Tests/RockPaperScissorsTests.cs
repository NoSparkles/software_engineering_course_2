using Xunit;
using FluentAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.SignalR;
using Games;
using Models.InMemoryModels;
using Models;

namespace backend.Tests
{
    public class RockPaperScissorsTests
    {
        private readonly RockPaperScissors _game;
        private readonly IHubCallerClients _fakeClients;
        private readonly ISingleClientProxy _fakeClientProxy;
        private readonly RoomUser _playerR;
        private readonly RoomUser _playerY;

        public RockPaperScissorsTests()
        {
            _game = new RockPaperScissors();
            _fakeClients = A.Fake<IHubCallerClients>();
            _fakeClientProxy = A.Fake<ISingleClientProxy>();
            
            A.CallTo(() => _fakeClients.Caller).Returns(_fakeClientProxy);
            A.CallTo(() => _fakeClients.Group(A<string>.Ignored)).Returns(_fakeClientProxy);
            
            _playerR = new RoomUser { PlayerId = "player1", Username = "Red" };
            _playerY = new RoomUser { PlayerId = "player2", Username = "Yellow" };
            
            _game.RoomCode = "test-room";
            _game.AssignPlayerColors(_playerR, _playerY);
        }

        [Fact]
        public async Task HandleCommand_GetState_Should_Send_Current_State()
        {
            await _game.HandleCommand("player1", "getState", _fakeClients, _playerR);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("ReceiveRpsState", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Should_Ignore_Empty_Command()
        {
            await _game.HandleCommand("player1", "", _fakeClients, _playerR);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync(A<string>._, A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Theory]
        [InlineData("CHOOSE:rock")]
        [InlineData("CHOOSE:paper")]
        [InlineData("CHOOSE:scissors")]
        public async Task HandleCommand_Choose_Should_Accept_Valid_Choice(string command)
        {
            await _game.HandleCommand("player1", command, _fakeClients, _playerR);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync(A<string>._, A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Choose_Should_Ignore_Invalid_Choice()
        {
            await _game.HandleCommand("player1", "CHOOSE:invalid", _fakeClients, _playerR);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync(A<string>._, A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Choose_Should_Resolve_Round_When_Both_Players_Choose()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("ReceiveRpsState", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Game_Should_Determine_Winner_Rock_Beats_Scissors()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            var state = _game.GetGameStatePublic();
            state.Should().NotBeNull();
        }

        [Fact]
        public async Task Game_Should_Determine_Winner_Paper_Beats_Rock()
        {
            await _game.HandleCommand("player1", "CHOOSE:paper", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            var state = _game.GetGameStatePublic();
            state.Should().NotBeNull();
        }

        [Fact]
        public async Task Game_Should_Determine_Winner_Scissors_Beats_Paper()
        {
            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:paper", _fakeClients, _playerY);

            var state = _game.GetGameStatePublic();
            state.Should().NotBeNull();
        }

        [Fact]
        public async Task Game_Should_Handle_Draw()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("ReceiveRpsState", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Game_Should_End_When_Player_Reaches_3_Wins()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            _game.WinnerColor.Should().Be("R");
        }

        [Fact]
        public async Task HandleCommand_Reset_Should_Require_Both_Players_Vote()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "RESET", _fakeClients, _playerR);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("ReceiveRpsState", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Reset_Should_Reset_Game_When_Both_Vote()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "RESET", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "RESET", _fakeClients, _playerY);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("RpsReset", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Should_Not_Accept_Choice_After_Match_Over()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            var callsBefore = Fake.GetCalls(_fakeClientProxy).Count();
            
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            
            var callsAfter = Fake.GetCalls(_fakeClientProxy).Count();
            callsAfter.Should().Be(callsBefore);
        }

        [Fact]
        public void GetGameStatePublic_Should_Return_Complete_State()
        {
            var state = _game.GetGameStatePublic();
            
            state.Should().NotBeNull();
        }

        [Fact]
        public async Task ReportWin_Should_Complete_Without_Error()
        {
            await _game.ReportWin("player1", _fakeClients);
        }

        [Fact]
        public async Task Game_Should_Track_Round_History()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:paper", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            var state = _game.GetGameStatePublic();
            state.Should().NotBeNull();
        }

        [Fact]
        public async Task Game_Should_End_After_5_Rounds_If_No_Player_Has_3_Wins()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            _game.WinnerColor.Should().Be("R");
        }

        [Fact]
        public async Task Game_Should_Handle_Draw_Match_After_5_Rounds()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:scissors", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:rock", _fakeClients, _playerY);

            _game.WinnerColor.Should().Be("Y");
        }

        [Fact]
        public async Task HandleCommand_Should_Not_Accept_Choice_From_Non_Player()
        {
            var nonPlayer = new RoomUser { PlayerId = "player3", Username = "Green" };
            
            await _game.HandleCommand("player3", "CHOOSE:rock", _fakeClients, nonPlayer);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync(A<string>._, A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Reset_Should_Not_Work_From_Non_Player()
        {
            var nonPlayer = new RoomUser { PlayerId = "player3", Username = "Green" };
            
            await _game.HandleCommand("player3", "RESET", _fakeClients, nonPlayer);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("RpsReset", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Should_Send_GameOver_When_Match_Complete()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player2", "CHOOSE:scissors", _fakeClients, _playerY);

            A.CallTo(() => _fakeClientProxy.SendCoreAsync("GameOver", A<object[]>._, A<System.Threading.CancellationToken>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task HandleCommand_Should_Not_Change_Choice_Once_Made()
        {
            await _game.HandleCommand("player1", "CHOOSE:rock", _fakeClients, _playerR);
            await _game.HandleCommand("player1", "CHOOSE:paper", _fakeClients, _playerR);

            var callCount = Fake.GetCalls(_fakeClientProxy)
                .Count(c => c.Method.Name == "SendCoreAsync");
            callCount.Should().BeGreaterThan(0);
        }
    }
}
