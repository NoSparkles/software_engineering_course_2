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
          // Don't navigate here - let MatchFound handle navigation
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
      connection.on("RoomClosed", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setTimeout(() => {
          navigate('/');
        }, 2000);
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

  // Cleanup when component unmounts
  useEffect(() => {
    return () => {
      if (connection && connection.state === "Connected") {
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("MatchmakingWaitingRoom: LeaveRoom failed on unmount:", err);
        });
      }
    };
  }, [connection, gameType, code, playerId]);

  // Handle navigation away from the page
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (connection && connection.state === "Connected") {
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("MatchmakingWaitingRoom: LeaveRoom failed on beforeunload:", err);
        });
      }
    };

    const handlePopState = () => {
      if (connection && connection.state === "Connected") {
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("MatchmakingWaitingRoom: LeaveRoom failed on popstate:", err);
        });
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
    <div className="matchmaking-waiting-room">
      <h1>Matchmaking Waiting Room</h1>
      <h2>Game: {gameType.toUpperCase()}</h2>
      <div className="matchmaking-info">
        <p>You are in matchmaking mode. The system will automatically pair you with another player.</p>
      </div>
    </div>
  );
}