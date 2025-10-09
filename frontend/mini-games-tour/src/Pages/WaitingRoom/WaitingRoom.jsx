import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useSignalRService } from '../../Utils/useSignalRService';
import { useAuth } from '../../Utils/AuthProvider'

export default function WaitingRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const playerId = usePlayerId();
  const { user, token } = useAuth()
  const [playerColor, setPlayerColor] = useState(null); 
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/joinByCodeHub",
    gameType,
    roomCode: code,
    playerId,
  });

  const [status, setStatus] = useState("Connecting...");

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      connection.invoke("Join", gameType, code, playerId, token)
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
          // Mark that we're transitioning to session room (not leaving the game)
          sessionStorage.setItem("transitioningToSession", "1");
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
        setPlayerColor(color[playerId]); 
      });

      connection.on("RoomClosed", (message, closedRoomKey) => {
        console.log("RoomClosed event received in WaitingRoom:", { message, closedRoomKey, thisComponentCode: code });
        
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
        const isInThisRoomByPath = currentPath.includes(`/waiting/${closedRoomCode}`);
        
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
            let finalStillInRoom = finalPath.includes(`/waiting/${closedRoomCode}`);
            
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
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("RoomClosed");
      };
    }
  }, [connection, connectionState, playerId, gameType, code, navigate, token]);

  return (
    <div className="waiting-room">
      <h1>Waiting Room</h1>
      <h2 className="status-message">Game: {gameType.toUpperCase()}</h2>
      <p>Room Code: {code}</p>
    </div>
  );
}