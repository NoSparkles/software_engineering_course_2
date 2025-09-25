import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useSignalRService } from '../../Utils/useSignalRService';

export default function WaitingRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const playerId = usePlayerId();
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

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color); 
        console.log(`Assigned color: ${color}`);
      });
    }
  }, [connection, connectionState, playerId, gameType, code, navigate]);

  return (
    <div className="waiting-room">
      <h2>Waiting Room</h2>
      <p>Game: <strong>{gameType.toUpperCase()}</strong></p>
      <p>Room Code: <strong>{code}</strong></p>
      <p>Player ID: <strong>{playerId}</strong></p>
      {gameType === "four-in-a-row" && (
      <p>
        Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
      </p>
    )}
      <p>Status: <strong>{status}</strong></p>
      <p>Connection: <strong>{connectionState}</strong></p>
    </div>
  );
}