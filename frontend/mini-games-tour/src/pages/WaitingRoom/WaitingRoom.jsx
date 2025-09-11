import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getConnection } from '../../utils/signalRService';
import './styles.css';

export default function WaitingRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState("Connecting...");

  useEffect(() => {
    const connection = getConnection();

    if (connection.state === "Disconnected") {
      connection.start()
        .then(() => {
          console.log("Connected to SignalR");
          setStatus("Connected. Joining room...");
          return connection.invoke("JoinRoom", gameType, code);
        })
        .catch(err => {
          console.error("Connection failed:", err);
          setStatus("Failed to connect.");
        });
    } else {
      // Already connected, just join the room
      connection.invoke("JoinRoom", gameType, code);
    }

    connection.on("WaitingForOpponent", () => {
      setStatus("Waiting for second player...");
    });

    connection.on("StartGame", (roomCode) => {
      if (roomCode === code) {
        setStatus("Opponent joined. Starting game...");
        navigate(`/${gameType}/session/${code}`);
      }
    });

    connection.onclose(() => {
      setStatus("Disconnected.");
    });

    connection.onreconnecting(() => {
      setStatus("Reconnecting...");
    });

    connection.onreconnected(() => {
      setStatus("Reconnected.");
    });

    return () => {
      // Optional: remove handlers to avoid memory leaks
      connection.off("WaitingForOpponent");
      connection.off("StartGame");
      connection.off("onclose");
      connection.off("onreconnecting");
      connection.off("onreconnected");
    };
  }, [gameType, code, navigate]);

  return (
    <div className="waiting-room">
      <h2>Waiting Room</h2>
      <p>Game: <strong>{gameType.toUpperCase()}</strong></p>
      <p>Room Code: <strong>{code}</strong></p>
      <p>Share this code with a friend to join.</p>
      <p>Status: <strong>{status}</strong></p>
    </div>
  );
}