import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useCountdownTimer } from '../../Utils/useCountdownTimer';
import PMBoard from '../../Games/PairMatchingGame/Components/GameBoard';
import RpsBoard from '../../Games/RockPaperScissors/Components/RpsBoard';
import {Board as FourInARowGameBoard} from '../../Games/FourInRowGame/Components/Board';
import { useAuth } from '../../Utils/AuthProvider';
import { globalConnectionManager } from '../../Utils/GlobalConnectionManager';
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
      const activeGameData = {
        gameType,
        code: code,
        playerId: playerId,
        isMatchmaking: true
      };
      localStorage.setItem("activeGame", JSON.stringify(activeGameData));
  }, [code, gameType, playerId]);

  // Register connection with global manager
  useEffect(() => {
    if (connection && connectionState === "Connected") {
      globalConnectionManager.registerConnection('matchmakingSessionRoom', connection, {
        gameType,
        roomCode: code,
        playerId
      });
      
      return () => {
        globalConnectionManager.unregisterConnection('matchmakingSessionRoom');
      };
    }
  }, [connection, connectionState, gameType, code, playerId]);

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

      connection.on("PlayerLeft", (leftPlayerId, message, roomCloseTime) => {
        setStatus(message);
        // Set room close time for countdown if provided
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
        } else {
          // Fallback: set 30 seconds from now
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
        }
      });

      connection.on("PlayerLeftRoom", (message, roomCloseTime) => {
        // This is for the player who left - set room close time for Return to Game banner
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
        } else {
          // Fallback: set 30 seconds from now
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
        }
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

      connection.on("RoomClosed", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
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
        // Call LeaveRoom when component unmounts (user navigates away)
        if (connection && connection.state === "Connected") {
          console.log("Component unmounting - calling LeaveRoom...");
          connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
            console.warn("LeaveRoom failed on unmount:", err);
          });
          console.log("LeaveRoom call initiated on unmount");
        }
        
        connection.off("WaitingForOpponent");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("PlayerLeftRoom");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("UnauthorizedMatchmaking");
        connection.off("PlayerDisconnected");
        connection.off("PlayerReconnected");
        connection.off("RoomClosing");
        connection.off("RoomClosed");
        connection.off("MatchmakingSessionEnded");
        connection.off("PlayerDeclinedReconnection");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);

  // Add beforeunload event listener to handle navigation away
  useEffect(() => {
    const handleBeforeUnload = () => {
      console.log("beforeunload event triggered in matchmaking room");
      if (connection && connection.state === "Connected") {
        console.log("Calling LeaveRoom on beforeunload in matchmaking room...");
        // Fire and forget - don't wait for response
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("LeaveRoom failed on beforeunload:", err);
        });
        console.log("LeaveRoom call initiated on beforeunload in matchmaking room");
      }
    };

    // Also listen for popstate events (browser back/forward)
    const handlePopState = () => {
      console.log("popstate event triggered in matchmaking room");
      if (connection && connection.state === "Connected") {
        console.log("Calling LeaveRoom on popstate in matchmaking room...");
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("LeaveRoom failed on popstate:", err);
        });
        console.log("LeaveRoom call initiated on popstate in matchmaking room");
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    window.addEventListener('popstate', handlePopState);
    
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
      window.removeEventListener('popstate', handlePopState);
    };
  }, [connection]);

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
      <div style={{ marginTop: '20px', textAlign: 'center' }}>
        <button 
        onClick={async () => {
          if (connection && connection.state === "Connected") {
            try {
              console.log("Calling EndMatchmakingSession...");
              await connection.invoke("EndMatchmakingSession", playerId);
              console.log("EndMatchmakingSession completed successfully");
            } catch (err) {
              console.warn("Failed to end session:", err);
            }
          }
        }}
          style={{ 
            backgroundColor: "#dc3545", 
            color: "white", 
            border: "none", 
            padding: "12px 24px", 
            borderRadius: "6px",
            cursor: "pointer",
            fontSize: "16px",
            fontWeight: "bold",
            boxShadow: "0 2px 4px rgba(0,0,0,0.2)"
          }}
        >
          🚪 End Session
        </button>
      </div>
      <div className="matchmaking-info">
        <p><em>You are playing in matchmaking mode. This game was automatically matched.</em></p>
        {timeLeft !== null ? (
          <p style={{ 
            color: timeLeft <= 10 ? "red" : timeLeft <= 20 ? "orange" : "black",
            fontWeight: "bold",
            marginTop: "10px"
          }}>
            {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
          </p>
        ) : (
          <p>The room will remain open until all players have left.</p>
        )}
      </div>
      <div className="game-board">
        {board}
      </div>
    </div>
  );
}