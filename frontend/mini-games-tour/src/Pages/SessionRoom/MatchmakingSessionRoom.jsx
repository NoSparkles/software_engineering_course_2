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

export default function MatchmakingSessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const { user, token } = useAuth()
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const [playerColor, setPlayerColor] = useState(null); // only for four-in-a-row
  const playerId = usePlayerId();
  const timeLeft = useCountdownTimer();
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/MatchMakingHub",
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
        isMatchmaking: true
      }));
  }, [code, gameType, playerId]);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      switch (gameType) {
          case 'rock-paper-scissors':
            setBoard(<RpsBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={false} token={token}/>);
            break;
          case 'four-in-a-row':
            setBoard(<FourInARowGameBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={false} token={token}/>);
            break;
          case 'pair-matching':
            setBoard(<PMBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={false} token={token}/>);
            break;
              default:
                  setBoard(null);
        }
    } else {
      setBoard(null);
    }
  }, [code, connection, connectionState, gameType, playerColor, playerId, token])

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      connection.invoke("Join", gameType, code, playerId, token)
        .then(() => setStatus("Joined matchmaking game session"))
        .catch(err => {
          console.error("Join matchmaking session failed:", err);
          setStatus("Failed to join matchmaking session.");
        });

      connection.on("WaitingForOpponent", () => {
        setStatus("Waiting for second player...");
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          setStatus("Game started! Good luck!");
        }
      });

      connection.on("PlayerLeft", () => {
        setStatus("Opponent disconnected. Game paused...");
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to matchmaking session.");
      });

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color[playerId]);
      });

      connection.on("UnauthorizedMatchmaking", () => {
        setStatus("Authentication failed. Redirecting to login...");
        navigate('/login');
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

      connection.on("MatchmakingSessionEnded", (message) => {
        setStatus(message);
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        setTimeout(() => {
          navigate('/');
        }, 2000);
      });

      connection.on("PlayerDeclinedReconnection", (declinedPlayerId, message) => {
        setStatus(message);
        // Store room close time for countdown
        localStorage.setItem("roomCloseTime", new Date(Date.now() + 30000).toISOString());
      });

      return () => {
        connection.off("WaitingForOpponent");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("UnauthorizedMatchmaking");
        connection.off("PlayerDisconnected");
        connection.off("PlayerReconnected");
        connection.off("RoomClosing");
        connection.off("MatchmakingSessionEnded");
        connection.off("PlayerDeclinedReconnection");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);

  return (
    <div className="matchmaking-session-room">
      <h2>{gameType.toUpperCase()} Matchmaking Session</h2>
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
      <button 
        onClick={() => {
          if (connection) {
            connection.invoke("EndMatchmakingSession", playerId);
          }
        }}
        style={{ 
          backgroundColor: "#dc3545", 
          color: "white", 
          border: "none", 
          padding: "8px 16px", 
          borderRadius: "4px",
          cursor: "pointer",
          marginTop: "10px",
          marginLeft: "10px"
        }}
      >
        End Session
      </button>
      <div className="matchmaking-info">
        <p><em>You are playing in matchmaking mode. This game was automatically matched.</em></p>
        {timeLeft !== null && (
          <p style={{ 
            color: timeLeft <= 10 ? "red" : timeLeft <= 20 ? "orange" : "black",
            fontWeight: "bold",
            marginTop: "10px"
          }}>
            {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
          </p>
        )}
      </div>
      <div className="game-board">
        {board}
      </div>
    </div>
  );
}