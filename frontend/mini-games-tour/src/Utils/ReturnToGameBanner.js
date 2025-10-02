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
    }
    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, [navigate]);
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const timeLeft = useCountdownTimer();
  
  // Debug logging
  useEffect(() => {
    console.log("ReturnToGameBanner: timeLeft changed to:", timeLeft);
  }, [timeLeft]);
  const location = useLocation();

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

  if (!showBanner || !gameInfo) return null;

  const handleReturnToGame = () => {
    const path = gameInfo.isMatchmaking 
      ? `/${gameInfo.gameType}/matchmaking-session/${gameInfo.code}`
      : `/${gameInfo.gameType}/session/${gameInfo.code}`;
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
