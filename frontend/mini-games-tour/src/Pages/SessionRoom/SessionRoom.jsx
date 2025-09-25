import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import PMBoard from '../../Games/PairMatchingGame/Components/GameBoard';
import RpsBoard from '../../Games/RockPaperScissors/Components/RpsBoard';
import {Board as FourInARowGameBoard} from '../../Games/FourInRowGame/Components/Board';

export default function SessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const query = new URLSearchParams(window.location.search);
  const isSpectator = query.get('spectator') === 'true';
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const [playerColor, setPlayerColor] = useState(null); // only for four-in-a-row
  const playerId = usePlayerId();
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/joinByCodeHub",
    gameType,
    roomCode: code,
    playerId,
  });

  useEffect(() => {
      localStorage.setItem("activeGame", JSON.stringify({
        gameType,
        code: code,
        playerId: playerId
      }));
  }, [code, gameType, playerId]);

  useEffect(() => {
  switch (gameType) {
    case 'rock-paper-scissors':
      setBoard(<RpsBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} />);
      break;
    case 'four-in-a-row':
      setBoard(<FourInARowGameBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator}/>);
      break;
    case 'pair-matching':
      setBoard(<PMBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator}/>);
      break;
        default:
            setBoard(null);
    }
  }, [gameType, playerColor, connection, connectionState]);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      if (isSpectator) {
        connection.invoke("JoinAsSpectator", gameType, code)
          .then(() => setStatus("Joined as spectator"))
          .catch(err => {
            console.error("JoinAsSpectator failed:", err);
            setStatus("Failed to join as spectator.");
          });
      } else {
        connection.invoke("JoinRoom", gameType, code, playerId)
          .then(() => setStatus("Joined room. Waiting for opponent..."))
          .catch(err => {
            console.error("JoinRoom failed:", err);
            setStatus("Failed to join room.");
          });
      }

      connection.on("WaitingForOpponent", () => {
        setStatus("Waiting for second player...");
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          setStatus("Opponent joined. Starting game...");
          navigate(`/${gameType}/session/${code}`);
        }
      });

      connection.on("PlayerLeft", () => {
        setStatus("Opponent disconnected. Waiting...");
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to room.");
      });

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color);
      });

      connection.on("SpectatorJoined", (roomCode) => {
        setStatus("Spectating room " + roomCode);
      });

      connection.on("SpectatorJoinFailed", (msg) => {
        setStatus("Spectator join failed: " + msg);
      });


      return () => {
        connection.off("PlayerLeft");
        connection.off("ReceiveMove");
        connection.off("onclose");
        connection.off("onreconnecting");
        connection.off("onreconnected");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId]);
  return (
    <div className="session-room">
      <h2>{gameType.toUpperCase()} Session</h2>
      <p>Room Code: <strong>{code}</strong></p>
      <p>Player ID: <strong>{playerId}</strong></p>
      <p>
        Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
      </p>
      <p>Status: <strong>{status}</strong></p>
      <p>Connection: <strong>{connectionState}</strong></p>
      <div className="game-board">
        {board}
      </div>
    </div>
  );
}