import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useSignalRService } from '../../Utils/useSignalRService';
import { useAuth } from '../../Utils/AuthProvider';
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
          setStatus("Opponent joined. Starting game...");
          navigate(`/${gameType}/matchmaking-session/${code}`);
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

      return () => {
        connection.off("WaitingForOpponent");
        connection.off("MatchFound");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("UnauthorizedMatchmaking");
      };
    }
  }, [connection, connectionState, playerId, gameType, code, navigate, token]);

  return (
    <div className="matchmaking-waiting-room">
      <h2>Matchmaking Waiting Room</h2>
      <p>Game: <strong>{gameType.toUpperCase()}</strong></p>
      <p>Player: <strong>{user?.username || playerId}</strong></p>
      {gameType === "four-in-a-row" && (
        <p>
          Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
        </p>
      )}
      <p>Status: <strong>{status}</strong></p>
      <p>Connection: <strong>{connectionState}</strong></p>
      <div className="matchmaking-info">
        <p><em>You are in matchmaking mode. The system will automatically pair you with another player.</em></p>
      </div>
    </div>
  );
}