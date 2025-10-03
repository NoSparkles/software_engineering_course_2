import { useEffect, useState, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import { useCountdownTimer } from './useCountdownTimer';
import './styles.css';

export default function ReturnToGameBanner() {
  const navigate = useNavigate();
  const location = useLocation();
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const [forceTimerReset, setForceTimerReset] = useState(0);
  const [roomCloseTime, setRoomCloseTime] = useState(() => localStorage.getItem("roomCloseTime"));
  const timeLeft = useCountdownTimer(forceTimerReset);
  const signalrRef = useRef([]);
  // Track if a player has left (for status message)
  const [playerLeft, setPlayerLeft] = useState(false);

  // Listen for storage events (cross-tab sync)
  useEffect(() => {
    function handleStorage(e) {
      if (e.key === "activeGame" && e.newValue === null) {
        setShowBanner(false);
        setGameInfo(null);
        if (window.location.pathname.includes("session") || window.location.pathname.includes("waiting")) {
          navigate('/');
        }
      }
      if (e.key === "roomCloseTime") {
        setRoomCloseTime(e.newValue);
        if (e.newValue === null) {
          setShowBanner(false);
          setForceTimerReset(x => x + 1);
        }
      }
      if (e.key === "PlayerLeft") {
        setPlayerLeft(true);
      }
      if (e.key === "PlayerReconnected") {
        // Always clear timer/banner and status for both players
        setShowBanner(false);
        setForceTimerReset(x => x + 1);
        setPlayerLeft(false);
      }
    }
    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, [navigate]);

  // Keep roomCloseTime in sync with localStorage
  useEffect(() => {
    setRoomCloseTime(localStorage.getItem("roomCloseTime"));
  }, [showBanner]);

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

      connection.on("PlayerLeft", () => {
        setPlayerLeft(true);
        localStorage.setItem("PlayerLeft", Date.now().toString());
        setTimeout(() => localStorage.removeItem("PlayerLeft"), 100);
      });
      connection.on("PlayerReconnected", () => {
        // Always clear timer/banner and status for both players
        setShowBanner(false);
        setForceTimerReset(x => x + 1);
        setGameInfo(null);
        setPlayerLeft(false);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
        localStorage.setItem("PlayerReconnected", Date.now().toString());
        setTimeout(() => localStorage.removeItem("PlayerReconnected"), 100);
      });
      connection.on("RoomClosed", function(reason, closedRoomCode) {
        // --- PATCH: Robustly ignore RoomClosed for old sessions using both sessionVersion and roomCode ---
        const sessionNow = localStorage.getItem("activeGame");
        const playerId = localStorage.getItem("playerId");
        const sessionVersion = sessionStorage.getItem("sessionVersion");
        if (!sessionNow || !playerId) return;
        const { gameType, code } = JSON.parse(sessionNow);

        // If the closedRoomCode is not for the current session, ignore
        if (
          closedRoomCode &&
          closedRoomCode !== code &&
          closedRoomCode !== `${gameType}:${code}`.toUpperCase()
        ) {
          return;
        }

        // PATCH: If the sessionVersion for the closedRoomCode does not match the current sessionVersion, ignore
        // This covers the case where the player rapidly switches sessions and the old RoomClosed arrives late
        const closedRoomVersion =
          sessionStorage.getItem(`sessionVersion:${closedRoomCode}`) ||
          sessionStorage.getItem(`sessionVersion:${(closedRoomCode || "").toUpperCase()}`) ||
          null;
        if (
          closedRoomVersion &&
          sessionVersion &&
          closedRoomVersion !== sessionVersion
        ) {
          return;
        }

        setShowBanner(false);
        setGameInfo(null);
        setForceTimerReset(x => x + 1);
        setPlayerLeft(false);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
      });
      connection.on("RoomClosing", () => {
        setShowBanner(false);
        setGameInfo(null);
        setForceTimerReset(x => x + 1);
        setPlayerLeft(false);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
      });

      connection.start().catch(() => {});
      connections.push(connection);
    });
    signalrRef.current = connections;

    return () => {
      connections.forEach(connection => {
        connection.off("PlayerLeft");
        connection.off("PlayerReconnected");
        connection.off("RoomClosed");
        connection.off("RoomClosing");
        connection.stop();
      });
    };
  }, []);

  // Check if room exists
  useEffect(() => {
    const session = localStorage.getItem("activeGame");
    if (!session) {
      setShowBanner(false);
      setGameInfo(null);
      return;
    }

    const { gameType, code, isMatchmaking } = JSON.parse(session);
    const pathSegments = location.pathname.toLowerCase().split('/');

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
      setGameInfo(null);
      return;
    }

    const checkRoom = async () => {
      try {
        const hubUrl = isMatchmaking
          ? "http://localhost:5236/MatchMakingHub"
          : "http://localhost:5236/joinByCodeHub";

        const connection = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .build();

        await connection.start();
        const { exists, isMatchmaking: roomIsMatchmaking } =
          await connection.invoke("RoomExistsWithMatchmaking", gameType, code);
        await connection.stop();

        if (exists) {
          setGameInfo({ gameType, code, isMatchmaking: roomIsMatchmaking });
          setShowBanner(true);
        } else {
          localStorage.removeItem("activeGame");
          setShowBanner(false);
          setGameInfo(null);
        }
      } catch {
        setShowBanner(false);
        setGameInfo(null);
      }
    };

    checkRoom();
  }, [location]);

  // Only show timer/banner if roomCloseTime is set (not null)
  if (!showBanner || !gameInfo || roomCloseTime === null) return null;

  const handleReturnToGame = () => {
    const path = gameInfo.isMatchmaking
      ? `/${gameInfo.gameType}/matchmaking-session/${gameInfo.code}`
      : `/${gameInfo.gameType}/session/${gameInfo.code}`;
    localStorage.removeItem("roomCloseTime");
    setRoomCloseTime(null);
    setShowBanner(false);
    navigate(path);
  };

  const handleDeclineReconnection = async () => {
    try {
      const session = localStorage.getItem("activeGame");
      if (session) {
        const { gameType, code, isMatchmaking } = JSON.parse(session);
        const playerId = localStorage.getItem("playerId");
        const token = localStorage.getItem("token");
        if (playerId && isMatchmaking && token) {
          const hubUrl = isMatchmaking
            ? "http://localhost:5236/MatchMakingHub"
            : "http://localhost:5236/joinByCodeHub";
          const connectionUrl = `${hubUrl}?access_token=${encodeURIComponent(token)}`;
          const connection = new HubConnectionBuilder()
            .withUrl(connectionUrl, {
              withCredentials: true,
              skipNegotiation: false,
              transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
            })
            .build();
          await connection.start();
          await connection.invoke("DeclineReconnection", playerId, gameType, code);
          await connection.stop();
        }
      }
    } catch (err) {
      console.error("Error declining reconnection:", err);
    } finally {
      localStorage.removeItem("activeGame");
      localStorage.removeItem("roomCloseTime");
      setRoomCloseTime(null);
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
      {playerLeft && (
        <p style={{ color: "#dc3545", fontWeight: "bold", marginTop: "10px" }}>
          Status: Player left the game
        </p>
      )}
      <div style={{ display: "flex", gap: "10px", marginTop: "10px" }}>
        <button onClick={handleReturnToGame}>
          Return to Game
        </button>
        {roomCloseTime !== null && (
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
      {roomCloseTime !== null && (
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

// --- PATCH: Export a helper to mark a new session version ---
export function markJustStartedNewSession(roomCode) {
  // Use a unique version for each session (timestamp)
  const version = Date.now().toString();
  sessionStorage.setItem("sessionVersion", version);
  if (roomCode) {
    sessionStorage.setItem(`sessionVersion:${roomCode}`, version);
    sessionStorage.setItem(`sessionVersion:${roomCode.toUpperCase()}`, version);
  }
}

// --- PATCH: Show a UI status/spinner for 1.7s when leaving a room (for both player A and B) ---
export function showLeaveRoomUiDelay() {
  return new Promise(resolve => setTimeout(resolve, 1700));
}
