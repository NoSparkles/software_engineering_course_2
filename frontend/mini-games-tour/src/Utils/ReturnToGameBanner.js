import { useEffect, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import { useCountdownTimer } from './useCountdownTimer';
import './styles.css';

export default function ReturnToGameBanner() {
  // Hide banner if 'activeGame' is removed from localStorage (e.g., RoomClosed event)
  const navigate = useNavigate();
  useEffect(() => {
    function handleStorage(e) {
      if (e.key === "activeGame" && e.newValue === null) {
        console.log("[ReturnToGameBanner][storage] Detected activeGame removal, hiding banner and navigating home.");
        setShowBanner(false);
        setGameInfo(null);
        // If user is on a session page, force navigation home
        if (window.location.pathname.includes("session") || window.location.pathname.includes("waiting")) {
          navigate('/');
        }
      }
      // NEW: Listen for PlayerReconnected event and clear timer/banner for both players
      if (e.key === "PlayerReconnected") {
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
      }
    }
    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, [navigate]);
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const [forceTimerReset, setForceTimerReset] = useState(0);
  const location = useLocation();
  const timeLeft = useCountdownTimer(forceTimerReset);
  
  // Debug logging
  useEffect(() => {
    console.log("ReturnToGameBanner: timeLeft changed to:", timeLeft);
  }, [timeLeft]);

  // Hide timer/banner immediately when both players are connected (roomCloseTime is null)
  useEffect(() => {
    // If roomCloseTime is null, hide banner and reset timer immediately
    if (showBanner && localStorage.getItem("roomCloseTime") === null) {
      setShowBanner(false);
      setForceTimerReset(x => x + 1);
    }
  }, [showBanner, timeLeft]);

  // Listen for PlayerReconnected, RoomClosed, and RoomClosing on both hubs (single effect)
  useEffect(() => {
    let connections = [];
    const session = localStorage.getItem("activeGame");
    if (!session) return;

    const hubUrls = [
      "http://localhost:5236/MatchMakingHub",
      "http://localhost:5236/joinByCodeHub"
    ];

    hubUrls.forEach(hubUrl => {
      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

      // When PlayerReconnected is received, always clear timer/banner immediately
      connection.on("PlayerReconnected", () => {
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setForceTimerReset(x => x + 1);
        setGameInfo(null);
        window.dispatchEvent(new StorageEvent("storage", { key: "roomCloseTime", oldValue: "something", newValue: null }));
      });
      connection.on("RoomClosed", () => {
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setShowBanner(false);
        setGameInfo(null);
        setForceTimerReset(x => x + 1);
        window.dispatchEvent(new StorageEvent("storage", { key: "roomCloseTime", oldValue: "something", newValue: null }));
        window.dispatchEvent(new StorageEvent("storage", { key: "activeGame", oldValue: "something", newValue: null }));
      });
      connection.on("RoomClosing", () => {
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setGameInfo(null);
        setForceTimerReset(x => x + 1);
        window.dispatchEvent(new StorageEvent("storage", { key: "roomCloseTime", oldValue: "something", newValue: null }));
      });

      connection.start().catch(() => {});
      connections.push(connection);
    });

    return () => {
      connections.forEach(connection => {
        connection.off("PlayerReconnected");
        connection.off("RoomClosed");
        connection.off("RoomClosing");
        connection.stop();
      });
    };
  }, []);

  useEffect(() => {
    const session = localStorage.getItem("activeGame");
    if (!session) return;

    const { gameType, code, isMatchmaking } = JSON.parse(session);
    const pathSegments = location.pathname.toLowerCase().split('/');

    // Check if path matches: /<gameType>/session/<code>, /<gameType>/waiting/<code>, /<gameType>/matchmaking-session/<code>, or /<gameType>/matchmaking-waiting/<code>
    const isSessionPath =
      pathSegments.length === 4 &&
      (pathSegments[2] === 'session' || 
       pathSegments[2] === "waiting" ||
       pathSegments[2] === "matchmaking-session" ||
       pathSegments[2] === "matchmaking-waiting") &&
      pathSegments[1] !== '' &&
      pathSegments[3] !== '';

    if (isSessionPath) {
      setShowBanner(false);
      return;
    }

    const checkRoom = async () => {
      try {
        // Use the correct hub based on whether it's a matchmaking room
        const hubUrl = isMatchmaking 
          ? "http://localhost:5236/MatchMakingHub"
          : "http://localhost:5236/joinByCodeHub";
        
        const connection = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .build();

        await connection.start();
        const { exists, isMatchmaking: roomIsMatchmaking } = await connection.invoke("RoomExistsWithMatchmaking", gameType, code);
        await connection.stop();

        if (exists) {
          setGameInfo({ gameType, code, isMatchmaking: roomIsMatchmaking });
          setShowBanner(true);
        } else {
          localStorage.removeItem("activeGame");
          setShowBanner(false);
        }
      } catch (err) {
        console.error("Room check failed:", err);
        setShowBanner(false);
      }
    };

    checkRoom();
  }, [location]);

  // Listen for local roomCloseTime removal in this tab (not just via storage event)
  useEffect(() => {
    const interval = setInterval(() => {
      if (showBanner && localStorage.getItem("roomCloseTime") === null) {
        setShowBanner(false);
        setForceTimerReset(x => x + 1); // force timer reset
      }
    }, 200); // faster polling for more immediate UI update
    return () => clearInterval(interval);
  }, [showBanner]);

  if (!showBanner || !gameInfo) return null;

  const handleReturnToGame = () => {
    const path = gameInfo.isMatchmaking 
      ? `/${gameInfo.gameType}/matchmaking-session/${gameInfo.code}`
      : `/${gameInfo.gameType}/session/${gameInfo.code}`;
    // NEW: Remove timer when returning to game (so both tabs clear)
    localStorage.removeItem("roomCloseTime");
    setShowBanner(false);
    navigate(path);
  };

  const handleDeclineReconnection = async () => {
    try {
      // Get player ID from active game
      const session = localStorage.getItem("activeGame");
      if (session) {
        const { gameType, code, isMatchmaking } = JSON.parse(session);
        const playerId = localStorage.getItem("playerId");
        const token = localStorage.getItem("token"); // Get the JWT token
      
        if (playerId && isMatchmaking && token) {
          // Use the correct hub based on whether it's a matchmaking room
          const hubUrl = isMatchmaking 
            ? "http://localhost:5236/MatchMakingHub"
            : "http://localhost:5236/joinByCodeHub";
        
          // Build connection URL with token as query parameter
          const connectionUrl = `${hubUrl}?access_token=${encodeURIComponent(token)}`;
        
          const connection = new HubConnectionBuilder()
            .withUrl(connectionUrl, {
              withCredentials: true,
              skipNegotiation: false,
              transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
            })
            .build();
          
          await connection.start();
          await connection.invoke("DeclineReconnection", playerId, gameType, code); // <-- This line changed
          await connection.stop();
        }
      }
    } catch (err) {
      console.error("Error declining reconnection:", err);
    } finally {
      localStorage.removeItem("activeGame");
      localStorage.removeItem("roomCloseTime");
      setShowBanner(false);
    }
  };

  const formatTime = (seconds) => {
    if (seconds === null) return "";
    if (seconds <= 0) return "Room closing now!";
    return `${seconds} seconds`;
  };

  return (
    <div className="return-banner">
      <p>You have an active game session.</p>
      <div style={{ display: "flex", gap: "10px", marginTop: "10px" }}>
        <button onClick={handleReturnToGame}>
          Return to Game
        </button>
        {timeLeft !== null && (
          <button 
            onClick={handleDeclineReconnection}
            style={{
              backgroundColor: "#dc3545",
              color: "white",
              border: "none",
              padding: "8px 16px",
              borderRadius: "4px",
              cursor: "pointer"
            }}
          >
            Decline Reconnection
          </button>
        )}
      </div>
      {timeLeft !== null && (
        <p style={{ 
          color: timeLeft <= 10 ? "red" : timeLeft <= 20 ? "orange" : "black",
          fontWeight: "bold",
          marginTop: "10px"
        }}>
          {timeLeft > 0 ? `Room will close in ${formatTime(timeLeft)}` : "Room is closing now!"}
        </p>
      )}
    </div>
  );
}


