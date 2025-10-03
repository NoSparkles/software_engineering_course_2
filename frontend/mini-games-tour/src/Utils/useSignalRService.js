import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { markJustStartedNewSession } from "./ReturnToGameBanner";

export function useSignalRService({ hubUrl, gameType, roomCode, playerId, token }) {
  const connectionRef = useRef(null);
  const [connectionState, setConnectionState] = useState("Disconnected");
  const [reconnected, setReconnected] = useState(false);

  useEffect(() => {
    if (!hubUrl || !playerId) return;

    // --- PATCH: Mark new session version ONLY after previous connections are stopped ---
    if (connectionRef.current) {
      connectionRef.current.stop();
      connectionRef.current = null;
    }

    // Now mark the new session version (guaranteed after cleanup)
    if (roomCode) {
      markJustStartedNewSession(roomCode);
    }

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
      console.log("[SignalR] Connection closed");
      setConnectionState("Disconnected");
    });

    connection.onreconnecting(() => {
      console.log("[SignalR] Reconnecting...");
      setConnectionState("Reconnecting");
    });

    connection.onreconnected(() => {
      console.log("[SignalR] Reconnected. Attempting to rejoin room...");
      setConnectionState("Connected");
      setReconnected(true);
      if (gameType && roomCode) {
        connection.invoke("Join", gameType, roomCode, playerId, token || "");
      }
    });

    connection
      .start()
      .then(() => {
        setConnectionState("Connected");
        console.log("[SignalR] Connected");
      })
      .catch(err => {
        setConnectionState("Failed");
        console.error("[SignalR] Connection failed:", err);
      });

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