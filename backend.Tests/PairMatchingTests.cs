using Xunit;
using FluentAssertions;
using Models;
using Microsoft.AspNetCore.SignalR;
using FakeItEasy;
using Games;
using Models.InMemoryModels;
using System.Text.Json;

namespace backend.Tests
{
    public class PairMatchingTests
    {
        private readonly PairMatching _game;
        private readonly ISingleClientProxy _callerProxy;
        private readonly IClientProxy _groupProxy;
        private readonly IHubCallerClients _clients;
        private readonly RoomUser _userR;
        private readonly RoomUser _userY;

        public PairMatchingTests()
        {
            _game = new PairMatching();
            _callerProxy = A.Fake<ISingleClientProxy>();  // Caller needs THIS type
            _groupProxy = A.Fake<IClientProxy>();         // Groups still return IClientProxy

            _clients = A.Fake<IHubCallerClients>();

            A.CallTo(() => _clients.Caller).Returns(_callerProxy);

            A.CallTo(() => _clients.Caller)
                .Returns(_callerProxy);


            _userR = new RoomUser("player1", true, new User { Username = "Red", PasswordHash = "hash" });
            _userY = new RoomUser("player2", true, new User { Username = "Yellow", PasswordHash = "hash" });
        }

        [Fact]
        public void Constructor_Should_InitializeBoardWith18Cards()
        {
            var board = _game.GetBoard();
            board.Length.Should().Be(18);
        }

        [Fact]
        public void GetGameState_Should_Return_Board_CurrentPlayer_Flipped_Scores_WinnerColor()
        {
            var state = _game.GetGameState();

            state.Should().NotBeNull();
            state.Board.Should().NotBeNull();
            state.CurrentPlayer.Should().NotBeNull();
            state.Flipped.Should().NotBeNull();
            state.Scores.Should().NotBeNull();
            state.Winner.Should().NotBeNull();
        }


        [Fact]
        public async Task HandleCommand_GetBoard_Should_SendBoardToCaller()
        {
            A.CallTo(() => _clients.Caller).Returns(_callerProxy);

            await _game.HandleCommand(_userR.PlayerId!, "getBoard", _clients, _userR);

            A.CallTo(() => _callerProxy.SendCoreAsync(
                "ReceiveBoard",
                A<object[]>.That.Matches(args => args.Length == 1),
                A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Flip_Should_FlipOneCardFaceUp()
        {
            await _game.HandleCommand(_userR.PlayerId!, "flip 0 0", _clients, _userR);

            var board = _game.GetBoard();
            board[0, 0].state.Should().Be(CardState.FaceUp);

        }

    }
}