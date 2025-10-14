import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { markJustStartedNewSession } from "./ReturnToGameBanner";

export function useSignalRService({ hubUrl, gameType, roomCode, playerId, token, isSpectator = false }) {
  const connectionRef = useRef(null);
  const [connectionState, setConnectionState] = useState("Disconnected");
  const [reconnected, setReconnected] = useState(false);
  const isCleaningUpRef = useRef(false);

  useEffect(() => {
    if (!hubUrl || !playerId) return;
    
    isCleaningUpRef.current = false;

    // Only clear old session data if we're starting a NEW session
    // Check if the stored activeGame is for a different room
    const existingGame = localStorage.getItem("activeGame");
    if (existingGame) {
      try {
        const gameData = JSON.parse(existingGame);
        // If it's a different room, clear the old data
        if (gameData.code !== roomCode || gameData.gameType !== gameType) {
          console.log("[SignalR] Clearing old session data for different room");
          localStorage.removeItem("roomCloseTime");
          localStorage.removeItem("activeGame");
        } else {
          console.log("[SignalR] Reconnecting to same room, keeping session data");
        }
      } catch (e) {
        // Invalid JSON, clear it
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
      }
    }
    
    sessionStorage.removeItem("leaveByHome");

    // Only mark activeGame for actual players, not spectators
    if (!isSpectator && roomCode && gameType) {
      localStorage.setItem("activeGame", JSON.stringify({
        gameType,
        code: roomCode,
        playerId,
        isMatchmaking: hubUrl.toLowerCase().includes("matchmaking")
      }));
    }

    // Build URL with query parameters for OnDisconnectedAsync
    let connectionUrl = hubUrl;
    if (playerId && gameType && roomCode) {
      const params = new URLSearchParams({
        playerId: playerId,
        gameType: gameType,
        roomCode: roomCode
      });
      connectionUrl = `${hubUrl}?${params.toString()}`;
      console.log("[SignalR] Connecting to:", connectionUrl);
    }

    const connection = new HubConnectionBuilder()
      .withUrl(connectionUrl, {
        withCredentials: true,
        skipNegotiation: true,
        transport: 1,
      })
      .configureLogging(LogLevel.Information)
      // Don't use automatic reconnect - it interferes with our disconnect/reconnect logic
      // .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.onclose((error) => {
      console.log("[SignalR] Connection closed", { error, isCleaningUp: isCleaningUpRef.current });
      setConnectionState("Disconnected");
      
      // Only set roomCloseTime if this is a clean shutdown (component unmounting due to navigation)
      // isCleaningUpRef.current is set to true when cleanup function runs (navigation away)
      const activeGame = localStorage.getItem("activeGame");
      // Spectators should never cause roomCloseTime to be set on navigation/cleanup
      if (!isSpectator && activeGame && !localStorage.getItem("roomCloseTime") && isCleaningUpRef.current) {
        const closeTime = new Date(Date.now() + 30000).toISOString();
        localStorage.setItem("roomCloseTime", closeTime);
        console.log("[SignalR] Set roomCloseTime for navigation away");
        
        // Delay the event dispatch to allow React Router navigation to complete
        setTimeout(() => {
          window.dispatchEvent(new Event("localStorageUpdate"));
          console.log("[SignalR] Dispatched localStorageUpdate after navigation delay");
        }, 100);
      } else {
        console.log("[SignalR] Not setting roomCloseTime", { 
          hasActiveGame: !!activeGame, 
          hasCloseTime: !!localStorage.getItem("roomCloseTime"),
          isCleaningUp: isCleaningUpRef.current 
        });
      }
    });

    // Removed automatic reconnection handlers since we disabled withAutomaticReconnect()
    // Manual reconnection is handled by user clicking "Return to Game" button

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
        // Check if we're transitioning to session room (waiting -> session)
        const isTransitioning = sessionStorage.getItem("transitioningToSession") === "1";
        
        if (isTransitioning) {
          console.log("[SignalR] Cleanup: transitioning to session room, keeping connection alive");
          sessionStorage.removeItem("transitioningToSession");
          // Don't stop the connection, don't mark as cleaning up
          // The new session room will reuse the connection context
          return;
        }
        
        console.log("[SignalR] Cleanup: stopping connection (navigation away)");
        // Mark that we're cleaning up (navigating away)
        isCleaningUpRef.current = true;
        
        // Stop the connection which will trigger onclose
        connectionRef.current.stop().then(() => {
          console.log("[SignalR] Connection stopped in cleanup");
        }).catch(err => {
          console.log("[SignalR] Error stopping connection:", err);
        });
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