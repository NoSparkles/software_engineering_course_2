import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useSignalRService } from '../../Utils/useSignalRService';
import { useAuth } from '../../Utils/AuthProvider';
import { globalConnectionManager } from '../../Utils/GlobalConnectionManager';
import './styles.css';

export default function MatchmakingWaitingRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const playerId = usePlayerId();
  const { user, token } = useAuth()
  const [playerColor, setPlayerColor] = useState(null); 
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/MatchMakingHub",
    gameType,
    roomCode: code,
    playerId,
  });

  const [status, setStatus] = useState("Connecting...");
  const connectionRegisteredRef = useRef(false);

  // Register connection immediately when available
  if (connection && connectionState === "Connected" && !connectionRegisteredRef.current) {
    globalConnectionManager.registerConnection('matchmakingWaitingRoom', connection, {
      gameType,
      roomCode: code,
      playerId
    });
    connectionRegisteredRef.current = true;
  }

  // Cleanup on unmount only
  useEffect(() => {
    return () => {
      globalConnectionManager.unregisterConnection('matchmakingWaitingRoom');
      connectionRegisteredRef.current = false;
    };
  }, []);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      // Rejoin the room to ensure we're properly connected to the SignalR group
      connection.invoke("Join", gameType, code, playerId, token)
        .then(() => setStatus("Connected to matchmaking room. Waiting for opponent..."))
        .catch(err => {
          console.error("Rejoin matchmaking room failed:", err);
          setStatus("Failed to rejoin matchmaking room.");
        });

      connection.on("WaitingForOpponent", (roomCode) => {
        setStatus("Waiting for second player to join matchmaking...");
      });

      connection.on("MatchFound", (roomCode) => {
        setStatus("Match found! Starting game...");
        navigate(`/${gameType}/matchmaking-session/${roomCode}`);
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          setStatus("Game starting...");
          // Mark that we're transitioning to session room (not leaving the game)
          sessionStorage.setItem("transitioningToSession", "1");
          navigate(`/${gameType}/matchmaking-session/${roomCode}`);
        }
      });

      connection.on("PlayerLeft", () => {
        setStatus("Opponent disconnected. Waiting for new match...");
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to matchmaking room.");
      });

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color[playerId]); 
      });

      connection.on("UnauthorizedMatchmaking", () => {
        setStatus("Authentication failed. Redirecting to login...");
        navigate('/login');
      });
      connection.on("RoomClosed", (message, closedRoomKey) => {
        console.log("RoomClosed event received in MatchmakingWaitingRoom:", { message, closedRoomKey, thisComponentCode: code });
        
        // Extract room code from closedRoomKey (format: "gameType:roomCode")
        const closedRoomCode = closedRoomKey ? closedRoomKey.split(':')[1] : code;
        console.log("Extracted closed room code:", closedRoomCode);
        
        // CRITICAL: First check if the closed room is even THIS component's room
        if (closedRoomCode !== code) {
          console.log(`RoomClosed is for different room (${closedRoomCode} vs ${code}), ignoring`);
          return;
        }
        
        // Multi-layer verification
        const currentPath = window.location.pathname;
        const isInThisRoomByPath = currentPath.includes(`/matchmaking-waiting/${closedRoomCode}`);
        
        const activeGameStr = localStorage.getItem("activeGame");
        let isInThisRoomByStorage = false;
        if (activeGameStr) {
          try {
            const activeGameData = JSON.parse(activeGameStr);
            isInThisRoomByStorage = activeGameData.code === closedRoomCode;
          } catch (e) {}
        }
        
        const shouldNavigate = isInThisRoomByPath && isInThisRoomByStorage && (closedRoomCode === code);
        
        if (shouldNavigate) {
          console.log("Player is CONFIRMED still in the waiting room that was closed, will navigate in 2 seconds");
          setStatus(message);
          localStorage.removeItem("roomCloseTime");
          localStorage.removeItem("activeGame");
          
          // Re-check before actually navigating
          setTimeout(() => {
            const finalPath = window.location.pathname;
            const finalActiveGame = localStorage.getItem("activeGame");
            let finalStillInRoom = finalPath.includes(`/matchmaking-waiting/${closedRoomCode}`);
            
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
            
            console.log("Final check passed, navigating to home");
            navigate('/');
          }, 2000);
        } else {
          console.log("Player has already left this waiting room, NOT navigating to home", {
            isInThisRoomByPath,
            isInThisRoomByStorage,
            codesMatch: closedRoomCode === code
          });
          if (isInThisRoomByStorage && closedRoomCode === code) {
            localStorage.removeItem("roomCloseTime");
            localStorage.removeItem("activeGame");
          }
        }
      });
      
      return () => {
        connection.off("WaitingForOpponent");
        connection.off("MatchFound");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("UnauthorizedMatchmaking");
        connection.off("RoomClosed");
      };
    }
  }, [connection, connectionState, playerId, gameType, code, navigate, token]);

  // Removed unmount, beforeunload, and popstate handlers
  // OnDisconnectedAsync handles disconnection and allows reconnection

  return (
    <div className="matchmaking-waiting-room page-shell">
      <div className="waiting-card card">
        <p className="eyebrow">Matchmaking</p>
        <h1>Finding your opponent</h1>
        <h2>Game: {gameType.toUpperCase()}</h2>
        <div className="matchmaking-info">
          <p>{status}</p>
          <p className="hint">Sit tight—once another player is ready you'll be dropped straight into the session.</p>
        </div>
      </div>
    </div>
  );
}