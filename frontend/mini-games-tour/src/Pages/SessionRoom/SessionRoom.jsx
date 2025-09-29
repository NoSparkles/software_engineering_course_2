import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useCountdownTimer } from '../../Utils/useCountdownTimer';
import PMBoard from '../../Games/PairMatchingGame/Components/GameBoard';
import RpsBoard from '../../Games/RockPaperScissors/Components/RpsBoard';
import {Board as FourInARowGameBoard} from '../../Games/FourInRowGame/Components/Board';
import { useAuth } from '../../Utils/AuthProvider';
import './styles.css';

export default function SessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const { user, token } = useAuth()
  const query = new URLSearchParams(window.location.search);
  const isSpectator = query.get('spectator') === 'true';
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const [playerColor, setPlayerColor] = useState(null); // only for four-in-a-row
  const playerId = usePlayerId();
  const timeLeft = useCountdownTimer();
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/joinByCodeHub",
    gameType,
    roomCode: code,
    playerId,
    token,
  });

  useEffect(() => {
      localStorage.setItem("activeGame", JSON.stringify({
        gameType,
        code: code,
        playerId: playerId,
        isMatchmaking: false
      }));
  }, [code, gameType, playerId]);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      switch (gameType) {
          case 'rock-paper-scissors':
            setBoard(<RpsBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
          case 'four-in-a-row':
            setBoard(<FourInARowGameBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
          case 'pair-matching':
            setBoard(<PMBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
              default:
                  setBoard(null);
        }
    } else {
      setBoard(null);
    }
  }, [code, connection, connectionState, gameType, isSpectator, playerColor, playerId, token])

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
        connection.invoke("Join", gameType, code, playerId, token)
          .then(() => setStatus("Joined room. Waiting for opponent..."))
          .catch(err => {
            console.error("Join failed:", err);
            setStatus("Failed to join room.");
          });
      }

      connection.on("WaitingForOpponent", () => {
        setStatus("Waiting for second player...");
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          setStatus("Game started! Good luck!");
        }
      });

      connection.on("PlayerLeft", () => {
        setStatus("Opponent disconnected. Waiting...");
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to room.");
      });

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color[playerId]);
      });

      connection.on("SpectatorJoined", (roomCode) => {
        setStatus("Spectating room " + roomCode);
      });

      connection.on("SpectatorJoinFailed", (msg) => {
        setStatus("Spectator join failed: " + msg);
      });

      connection.on("PlayerDisconnected", (disconnectedPlayerId, message, roomCloseTime) => {
        setStatus(message);
        // Store room close time for countdown
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
        }
      });

      connection.on("PlayerReconnected", (reconnectedPlayerId, message) => {
        setStatus(message);
        // Clear room close time when player reconnects
        localStorage.removeItem("roomCloseTime");
      });

      connection.on("RoomClosing", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        setTimeout(() => {
          navigate('/');
        }, 2000);
      });

      return () => {
        connection.off("WaitingForOpponent");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("SpectatorJoined");
        connection.off("SpectatorJoinFailed");
        connection.off("PlayerDisconnected");
        connection.off("PlayerReconnected");
        connection.off("RoomClosing");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);
  return (
    <div className="session-room">
      <h2>{gameType.toUpperCase()} Session</h2>
      <p>Room Code: <strong>{code}</strong></p>
      <p>Player: <strong>{user?.username || playerId}</strong></p>
      <p>
        Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
      </p>
      <p>Status: <strong>{status}</strong></p>
      <p>Connection: <strong style={{
        color: connectionState === "Connected" ? "green" : 
               connectionState === "Reconnecting" ? "orange" : 
               connectionState === "Disconnected" ? "red" : "gray"
      }}>{connectionState}</strong></p>
      {connectionState === "Disconnected" && (
        <button 
          onClick={() => {
            if (connection) {
              connection.start().catch(err => console.error("Reconnection failed:", err));
            }
          }}
          style={{ 
            backgroundColor: "#007bff", 
            color: "white", 
            border: "none", 
            padding: "8px 16px", 
            borderRadius: "4px",
            cursor: "pointer",
            marginTop: "10px"
          }}
        >
          Reconnect
        </button>
      )}
      {timeLeft !== null && (
        <div style={{ 
          textAlign: "center",
          margin: "10px 0",
          padding: "10px",
          backgroundColor: timeLeft <= 10 ? "#ffebee" : timeLeft <= 20 ? "#fff3e0" : "#f5f5f5",
          borderRadius: "4px"
        }}>
          <p style={{ 
            color: timeLeft <= 10 ? "red" : timeLeft <= 20 ? "orange" : "black",
            fontWeight: "bold",
            margin: 0
          }}>
            {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
          </p>
        </div>
      )}
      <div className="game-board">
        {board}
      </div>
    </div>
  );
}