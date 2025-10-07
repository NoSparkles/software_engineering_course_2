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
    sessionStorage.removeItem("leaveByHome");

    // Always set activeGame when joining a new session
    if (roomCode && gameType) {
      localStorage.setItem("activeGame", JSON.stringify({
        gameType,
        code: roomCode,
        playerId,
        isMatchmaking: hubUrl.toLowerCase().includes("matchmaking")
      }));
      // DO NOT set fallback roomCloseTime - let backend handle timer logic
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
      console.log("[SignalR] Reconnected.");
      setConnectionState("Connected");
      setReconnected(true);
      // DO NOT automatically rejoin - let the room components handle rejoining
      // This prevents excessive reconnections and timer issues
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

    // PATCH: Disconnect on logout
    function handleLogout(e) {
      if (e.key === "token" && e.newValue === null && connectionRef.current) {
        connectionRef.current.stop();
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
      }
    }
    window.addEventListener("storage", handleLogout);

    return () => {
      window.removeEventListener("storage", handleLogout);
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