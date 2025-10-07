import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { markJustStartedNewSession } from "./ReturnToGameBanner";

export function useSignalRService({ hubUrl, gameType, roomCode, playerId, token }) {
  const connectionRef = useRef(null);
  const [connectionState, setConnectionState] = useState("Disconnected");
  const [reconnected, setReconnected] = useState(false);

  useEffect(() => {
    if (!hubUrl || !playerId) return;

    // Always clear roomCloseTime and activeGame when joining a new session
    localStorage.removeItem("roomCloseTime");
    localStorage.removeItem("activeGame");

    // Always set activeGame and roomCloseTime when joining a new session
    if (roomCode && gameType) {
      localStorage.setItem("activeGame", JSON.stringify({
        gameType,
        code: roomCode,
        playerId,
        isMatchmaking: hubUrl.toLowerCase().includes("matchmaking")
      }));
      // PATCH: If joining a session, set a fallback roomCloseTime if not present
      if (!localStorage.getItem("roomCloseTime")) {
        const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
        localStorage.setItem("roomCloseTime", fallbackCloseTime);
      }
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