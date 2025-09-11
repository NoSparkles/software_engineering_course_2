import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../utils/useSignalRService';
import { usePlayerId } from '../../utils/usePlayerId';
import PMBoard from '../../games/PairMatchingGame/components/GameBoard';
import {Board as FourInARowGameBoard} from '../../games/fourInRowGame/components/Board';

export default function SessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const playerId = usePlayerId();
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/gamehub",
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
            setBoard(undefined); // placeholder for Dominykas
            break;
        case 'four-in-a-row':
            setBoard(<FourInARowGameBoard/>);
            break;
        case 'pair-matching':
            setBoard(<PMBoard />);
            break;
        default:
            setBoard(null);
    }
  }, [gameType]);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      connection.invoke("JoinRoom", gameType, code, playerId)
        .then(() => setStatus("Joined room. Waiting for opponent..."))
        .catch(err => {
          console.error("JoinRoom failed:", err);
          setStatus("Failed to join room.");
        });

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
      <p>Status: <strong>{status}</strong></p>

      <div className="game-board">
        {board}
      </div>
    </div>
  );
}