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
  const query = new URLSearchParams(window.location.search);
  const isSpectator = query.get('spectator') === 'true';
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
    // PATCH: Set fallback roomCloseTime if not present
    if (!localStorage.getItem("roomCloseTime")) {
      const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
      localStorage.setItem("roomCloseTime", fallbackCloseTime);
    }
  }, [code, gameType, playerId]);

  // Register connection with global manager
  useEffect(() => {
    if (connection) {
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
        });
      });

      connection.on("RoomClosed", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setTimeout(() => {
          navigate('/');
        }, 2000);
      });

      // Failsafe: Listen for localStorage removal of activeGame (in case RoomClosed event is missed)
      const handleStorage = (e) => {
        if (e.key === "activeGame" && e.newValue === null) {
          console.log("[RoomClosed][storage] Detected activeGame removal, forcing navigation.");
          localStorage.removeItem("roomCloseTime");
          navigate('/');
        }
      };
      window.addEventListener("storage", handleStorage);

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
        if (!isSpectator && connection && connection.state === "Connected") {
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
        connection.off("SpectatorJoined");
        connection.off("SpectatorJoinFailed");
        connection.off("PlayerDisconnected");
        connection.off("PlayerReconnected");
        connection.off("RoomClosing");
        connection.off("RoomClosed");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);

  // Block navigation and enforce delay when leaving session room
  useEffect(() => {
      const handleBeforeUnload = () => {
        console.log("beforeunload event triggered");
        if (!isSpectator && connection && connection.state === "Connected") {
          console.log("Calling LeaveRoom on beforeunload...");
          // Fire and forget - don't wait for response
          connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
            console.warn("LeaveRoom failed on beforeunload:", err);
          });
          console.log("LeaveRoom call initiated on beforeunload");
        }
      };
  
      // Also listen for popstate events (browser back/forward)
    const handlePopState = () => {
      console.log("popstate event triggered");
      if (!isSpectator && connection && connection.state === "Connected") {
        console.log("Calling LeaveRoom on popstate...");
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("LeaveRoom failed on popstate:", err);
        });
        console.log("LeaveRoom call initiated on popstate");
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    window.addEventListener('popstate', handlePopState);
    
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
      window.removeEventListener('popstate', handlePopState);
    };
  }, [connection, isSpectator]);
  

  const handleLeaveRoom = async () => {
    if (!isSpectator && connection && connection.state === "Connected") {
      try {
        await connection.invoke("LeaveRoom", gameType, code, playerId);
        // PATCH: Dispatch event to force ReturnToGameBanner to re-check backend after leave
        window.dispatchEvent(new Event("LeaveRoomBannerCheck"));
      } catch (err) {
        console.warn("LeaveRoom failed:", err);
      }
    }
    navigate('/');
  };

  return (
    <div className="session-room">
      <h2>{gameType.toUpperCase()} Matchmaking Session</h2>
      <p>Player: <strong>{user?.username || playerId}</strong></p>
      <p>
        Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
      </p>
      
      <p>Connection: <strong style={{
        color: connectionState === "Connected" ? "green" : 
               connectionState === "Reconnecting" ? "orange" : 
               connectionState === "Disconnected" ? "red" : "gray"
      }}>{connectionState}</strong></p>
      {connectionState === "Disconnected" && (
        <button className="reconnect-btn" onClick={() => {
          if (connection) connection.start().catch(err => console.error("Reconnection failed:", err));
        }}>
          Reconnect
        </button>
      )}
      <div style={{ marginTop: '20px', textAlign: 'center' }}>
        <button onClick={handleLeaveRoom}>
          🚪 Leave Room
        </button>
      </div>

      {timeLeft !== null ? (
        <div className={`time-left ${timeLeft <= 10 ? 'short' : timeLeft <= 20 ? 'medium' : 'long'}`}>
          {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
        </div>
      ) : (
        <div className="time-left stable">
          The room will remain open until all players have left.
        </div>
      )}

      <div className="game-board">
        {board}
      </div>
    </div>
  );
}