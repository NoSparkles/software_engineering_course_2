import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function useSignalRService({ hubUrl, gameType, roomCode, playerId }) {
  const connectionRef = useRef(null);
  const [connectionState, setConnectionState] = useState("Disconnected");
  const [reconnected, setReconnected] = useState(false);

  useEffect(() => {
    if (!hubUrl || !playerId) return;

    if (!connectionRef.current) {
      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          withCredentials: true,
          skipNegotiation: true,
          transport: 1,
        })
        .configureLogging(LogLevel.Information)
        .withAutomaticReconnect()
        .build();

      connectionRef.current = connection;

      connection.onclose(() => {
        setConnectionState("Disconnected");
      });

      connection.onreconnecting(() => setConnectionState("Reconnecting"));

      connection.onreconnected(() => {
        setConnectionState("Connected");
        setReconnected(true);
        if (gameType && roomCode) {
          connection.invoke("ReconnectToRoom", gameType, roomCode, playerId);
        }
      });

      connection
        .start()
        .then(() => {
          setConnectionState("Connected");
          console.log("SignalR connected");
        })
        .catch(err => {
          setConnectionState("Failed");
          console.error("SignalR connection failed:", err);
        });
    }

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
        connectionRef.current = null;
      }
    };
  }, [hubUrl, playerId, gameType, roomCode]);

  return {
    connection: connectionRef.current,
    connectionState,
    reconnected,
  };
}