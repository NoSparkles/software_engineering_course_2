import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useCountdownTimer } from '../../Utils/useCountdownTimer';
import PMBoard from '../../Games/PairMatchingGame/Components/GameBoard';
import RpsBoard from '../../Games/RockPaperScissors/Components/RpsBoard';
import {Board as FourInARowGameBoard} from '../../Games/FourInRowGame/Components/Board';
import { useAuth } from '../../Utils/AuthProvider';
import { globalConnectionManager } from '../../Utils/GlobalConnectionManager';
import { markLeaveByHome } from '../../Utils/ReturnToGameBanner';
import './styles.css';

export default function MatchmakingSessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const { user, token } = useAuth()
  const query = new URLSearchParams(window.location.search);
  const isSpectator = query.get('spectator') === 'true';
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const [playerColor, setPlayerColor] = useState(null);
  const playerColorRef = useRef(null);
  const playerId = usePlayerId();
  const timeLeft = useCountdownTimer();
  const [isLeavingRoom, setIsLeavingRoom] = useState(false);
  const hasLeftRoomRef = useRef(false);
  const [roomCloseTime, setRoomCloseTime] = useState(() => localStorage.getItem("roomCloseTime"));
  const [roomPlayers, setRoomPlayers] = useState([playerId]);
  const [gameStarted, setGameStarted] = useState(false);
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/MatchMakingHub",
    gameType,
    roomCode: code,
    playerId,
    token,
    isSpectator,
  });

  useEffect(() => {
    const activeGameData = {
      gameType,
      code: code,
      playerId: playerId,
      isMatchmaking: true
    };
    localStorage.setItem("activeGame", JSON.stringify(activeGameData));
    
    // Clear roomCloseTime when entering a room (fresh start)
    localStorage.removeItem("roomCloseTime");
    setRoomCloseTime(null);
  }, [code, gameType, playerId]);

  useEffect(() => {
    if (connection) {
      globalConnectionManager.registerConnection('matchmakingSessionRoom', connection, {
        gameType,
        roomCode: code,
        playerId
      });
      
      return () => {
        // Mark that we're cleaning up this component (navigating away)
        hasLeftRoomRef.current = true;
        console.log("MatchmakingSessionRoom cleanup - marking hasLeftRoom as true");
        globalConnectionManager.unregisterConnection('matchmakingSessionRoom');
      };
    }
  }, [connection, connectionState, gameType, code, playerId]);

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
      // Set up RoomPlayersUpdate listener FIRST before joining
      connection.on("RoomPlayersUpdate", (players) => {
        console.log("RoomPlayersUpdate received:", players);
        setRoomPlayers(players);
      });

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

      connection.on("MatchFound", (roomCode) => {
        setStatus("Match found! Game starting...");
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          console.log("StartGame event received, clearing timers");
          setStatus("Game started! Good luck!");
          localStorage.removeItem("roomCloseTime");
          setRoomCloseTime(null);
          setGameStarted(true);
        }
      });

      connection.on("PlayerLeft", (leftPlayerId, message, roomCloseTime) => {
        setStatus(message);
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
          setRoomCloseTime(roomCloseTime);
        } else {
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
          setRoomCloseTime(fallbackCloseTime);
        }
      });

      connection.on("PlayerLeftRoom", (message, roomCloseTime) => {
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
        } else {
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
        }
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to matchmaking session.");
        if (connection && connectionState === "Connected" && !isSpectator) {
          connection.invoke("Join", gameType, code, playerId, token)
            .then(() => setStatus("Rejoined matchmaking session"))
            .catch(err => console.error("Rejoin failed:", err));
        }
      });

      connection.on("SetPlayerColor", (color) => {
        const extractedColor = color[playerId];
        setPlayerColor(color[playerId]);
        playerColorRef.current = extractedColor;
      });

      connection.on("GameOver", (winnerColor) => {
        const currentPlayerColor = playerColorRef.current;
        console.log("GameOver event:", { winnerColor, playerColor: currentPlayerColor, playerId });
        
        if (!isSpectator) {
          if (winnerColor === "DRAW") {
            setStatus("It's a draw!");
            console.log("Game ended in a draw");
          } else if (currentPlayerColor === winnerColor) {
            setStatus("You won!");
            if (connection && connection.state === "Connected") {
              connection.invoke("ReportWin", gameType, code, playerId)
                .catch(err => console.error("ReportWin failed:", err));
            }
            console.log(`Player ${playerId} with color ${currentPlayerColor} has won the game`);
          } else {
            setStatus("You lost!");
            console.log(`Player ${playerId} with color ${currentPlayerColor} has lost the game. Winner was ${winnerColor}`);
          }
        } else {
          // For spectators, show more descriptive winner messages
          if (winnerColor === "DRAW") {
            setStatus("Game over! It's a draw!");
          } else {
            // Handle different game types for spectator display
            let winnerName = winnerColor;
            if (gameType === 'four-in-a-row' || gameType === 'pair-matching') {
              winnerName = winnerColor === "R" ? "Red" : "Yellow";
            } else if (gameType === 'rock-paper-scissors') {
              winnerName = winnerColor === "R" ? "Player 1 (Red)" : "Player 2 (Yellow)";
            }
            setStatus(`Game over! Winner: ${winnerName}`);
          }
        }
      });

      connection.on("UnauthorizedMatchmaking", () => {
        setStatus("Authentication failed. Redirecting to login...");
        navigate('/login');
      });

      connection.on("PlayerDisconnected", (disconnectedPlayerId, message, roomCloseTime) => {
        console.log("PlayerDisconnected event received:", { disconnectedPlayerId, message, roomCloseTime });
        setStatus(message);
        if (roomCloseTime) {
          console.log("Setting roomCloseTime from backend:", roomCloseTime);
          localStorage.setItem("roomCloseTime", roomCloseTime);
          setRoomCloseTime(roomCloseTime);
        } else {
          console.log("No roomCloseTime from backend, using fallback");
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
          setRoomCloseTime(fallbackCloseTime);
        }
      });

      connection.on("PlayerReconnected", (reconnectedPlayerId, message) => {
        console.log("PlayerReconnected event received:", { reconnectedPlayerId, message });
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
        setGameStarted(true); // Resume game when player reconnects
      });

      connection.on("RoomClosing", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        setTimeout(() => {
          navigate('/');
        });
      });

      connection.on("JoinFailed", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setTimeout(() => navigate('/'), 2000);
      });

      connection.on("RoomClosed", (message, closedRoomKey) => {
        console.log("RoomClosed event received:", { message, closedRoomKey, thisComponentCode: code, hasLeftRoom: hasLeftRoomRef.current });
        
        // Check session storage flag (set when declining reconnection or starting new search)
        if (sessionStorage.getItem(`hasLeftRoom_${code}`) === "1") {
          console.log("Player has declined reconnection for this room, ignoring RoomClosed");
          sessionStorage.removeItem(`hasLeftRoom_${code}`);
          return;
        }
        
        // If we've already intentionally left this room, don't navigate
        if (hasLeftRoomRef.current) {
          console.log("Player has already left this room intentionally, ignoring RoomClosed");
          return;
        }
        
        // Extract room code from closedRoomKey (format: "gameType:roomCode")
        const closedRoomCode = closedRoomKey ? closedRoomKey.split(':')[1] : code;
        console.log("Extracted closed room code:", closedRoomCode);
        
        // CRITICAL: First check if the closed room is even THIS component's room
        // This prevents old component instances from reacting to wrong room closures
        if (closedRoomCode !== code) {
          console.log(`RoomClosed is for different room (${closedRoomCode} vs ${code}), ignoring`);
          return;
        }
        
        // Multi-layer verification to ensure we only navigate if still in THIS room
        const currentPath = window.location.pathname;
        const isInThisRoomByPath = 
          currentPath.includes(`/matchmaking-session/${closedRoomCode}`) || 
          currentPath.includes(`/matchmaking-waiting/${closedRoomCode}`);
        
        // Also check activeGame in localStorage
        const activeGameStr = localStorage.getItem("activeGame");
        let isInThisRoomByStorage = false;
        if (activeGameStr) {
          try {
            const activeGameData = JSON.parse(activeGameStr);
            isInThisRoomByStorage = activeGameData.code === closedRoomCode;
            console.log("ActiveGame check:", { 
              storedCode: activeGameData.code, 
              closedCode: closedRoomCode, 
              matches: isInThisRoomByStorage 
            });
          } catch (e) {}
        }
        
        // ALL checks must pass to navigate
        const shouldNavigate = isInThisRoomByPath && isInThisRoomByStorage && (closedRoomCode === code);
        
        if (shouldNavigate) {
          console.log("Player is CONFIRMED still in the room that was closed, will navigate in 2 seconds");
          setStatus(message);
          localStorage.removeItem("roomCloseTime");
          localStorage.removeItem("activeGame");
          
          // Re-check before actually navigating (in case player leaves during the 2-second delay)
          setTimeout(() => {
            // Check again if we're still in the same room
            const finalPath = window.location.pathname;
            const finalActiveGame = localStorage.getItem("activeGame");
            let finalStillInRoom = finalPath.includes(`/matchmaking-session/${closedRoomCode}`);
            
            if (finalActiveGame) {
              try {
                const finalGameData = JSON.parse(finalActiveGame);
                if (finalGameData.code !== closedRoomCode) {
                  console.log("Player moved to different room during delay, NOT navigating");
                  return;
                }
              } catch (e) {}
            }
            
            if (!finalStillInRoom) {
              console.log("Player left room during delay, NOT navigating");
              return;
            }
            
            if (hasLeftRoomRef.current) {
              console.log("Player marked as left during delay, NOT navigating");
              return;
            }
            
            console.log("Final check passed, navigating to home");
            navigate('/');
          }, 2000);
        } else {
          console.log("Player has already left this room, NOT navigating to home", {
            isInThisRoomByPath,
            isInThisRoomByStorage,
            codesMatch: closedRoomCode === code
          });
          // Just clear the storage for this closed room if it matches
          if (isInThisRoomByStorage && closedRoomCode === code) {
            localStorage.removeItem("roomCloseTime");
            localStorage.removeItem("activeGame");
          }
        }
      });

      const handleStorage = (e) => {
        if (e.key === "activeGame" && e.newValue === null) {
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
        localStorage.setItem("roomCloseTime", new Date(Date.now() + 30000).toISOString());
      });

      return () => {
        // Don't call LeaveRoom here - OnDisconnectedAsync will handle it
        // and mark player as disconnected (allowing reconnection)
        connection.off("RoomPlayersUpdate");
        connection.off("WaitingForOpponent");
        connection.off("MatchFound");
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
        connection.off("JoinFailed");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);

  // Removed beforeunload and popstate handlers - OnDisconnectedAsync handles disconnection
  // and allows reconnection. Only explicit "Leave Room" button press should close room.
  

  const handleLeaveRoom = async () => {
    if (isLeavingRoom) {
      return;
    }
  
    setIsLeavingRoom(true);
    hasLeftRoomRef.current = true; // Mark that we're intentionally leaving
  
    if (!isSpectator && connection && connection.state === "Connected") {
      try {
        // LeaveRoom will close the room immediately for all players
        await connection.invoke("LeaveRoom", gameType, code, playerId);
        
        // Clear any reconnection data since room is closed
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        sessionStorage.removeItem("lastActiveGame");
        sessionStorage.removeItem("lastRoomCloseTime");
        
        window.dispatchEvent(new Event("LeaveRoomBannerCheck"));
      } catch (err) {
        console.warn("LeaveRoom failed:", err);
        setIsLeavingRoom(false);
        hasLeftRoomRef.current = false;
        return;
      }
    }
    
    navigate('/');
  };

  const showTimer = !isSpectator &&
    roomCloseTime &&
    Date.parse(roomCloseTime) > Date.now() &&
    timeLeft !== null &&
    timeLeft > 0;

  // Debug logging
  useEffect(() => {
    console.log("Timer state:", { roomCloseTime, timeLeft, showTimer, isSpectator });
  }, [roomCloseTime, timeLeft, showTimer, isSpectator]);

  return (
    <div className="session-room ">
      <div className="session-header ">
        <p className="eyebrow">Matchmaking session</p>
        <h2>{gameType.toUpperCase().replace(/-/g, ' ')} Matchmaking Session</h2>
        <p className="session-role">Player: <strong>{user?.username || playerId}</strong></p>
        <p className="session-role">
          {isSpectator ? (
            <>Role: <strong>Spectator</strong></>
          ) : (
            <>Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong></>
          )}
        </p>
        
        <p className="session-role">
          Connection: <strong style={{
            color: connectionState === "Connected" ? "#7df5bd" : 
                   connectionState === "Reconnecting" ? "#ffcf7a" : 
                   connectionState === "Disconnected" ? "#ff9fb1" : "#9fb0d1"
          }}>{connectionState}</strong>
        </p>
        
        <div className="session-actions">
          {connectionState === "Disconnected" && (
            <button className="btn btn--ghost" onClick={() => {
              if (connection) connection.start().catch(err => console.error("Reconnection failed:", err));
            }}>
              Reconnect
            </button>
          )}
          <button 
            className="btn btn--primary session-leave"
            onClick={handleLeaveRoom} 
            disabled={isLeavingRoom}
          >
            {isLeavingRoom ? 'Leaving...' : '🚪 Leave Room'}
          </button>
        </div>

        {showTimer ? (
          <div className={`time-left ${timeLeft <= 10 ? 'short' : timeLeft <= 20 ? 'medium' : 'long'}`}>
            {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
          </div>
        ) : (
          <div className="time-left stable">
            The room will remain open until all players have left.
          </div>
        )}
      </div>

      <div className="game-board ">
        {(connectionState === "Connected" && board) ? (
          board
        ) : (
          <div className="board-placeholder">
            🔌 Connecting to game...
          </div>
        )}
      </div>
    </div>
  );
}