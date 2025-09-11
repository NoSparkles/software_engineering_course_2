import React, { useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import './styles.css';

export default function WaitingRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const connectionRef = useRef(null);
  const [status, setStatus] = useState("Connecting...");

  useEffect(() => {
    // Prevent duplicate connections
    if (connectionRef.current) return;

    const connection = new HubConnectionBuilder()
      .withUrl("http://localhost:5236/gamehub", {
        withCredentials: true,
        skipNegotiation: true,
        transport: 1 // WebSockets
      })
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.start()
      .then(() => {
        console.log("Connected to SignalR");
        setStatus("Connected. Joining room...");
        return connection.invoke('JoinRoom', gameType, code);
      })
      .catch(err => {
        console.error("Connection failed:", err);
        setStatus("Failed to connect.");
      });

    connection.on('WaitingForOpponent', () => {
      setStatus("Waiting for second player...");
    });

    connection.on('StartGame', (roomCode) => {
      if (roomCode === code) {
        setStatus("Opponent joined. Starting game...");
        navigate(`/${gameType}/session/${code}`);
      }
    });

    connection.onclose(error => {
      console.error("Connection closed:", error);
      setStatus("Disconnected.");
    });

    connection.onreconnecting(error => {
      console.warn("Reconnecting:", error);
      setStatus("Reconnecting...");
    });

    connection.onreconnected(connectionId => {
      console.log("Reconnected:", connectionId);
      setStatus("Reconnected.");
    });

    // Optional cleanup if you want to disconnect on unmount
    // return () => {
    //   connection.stop();
    // };
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