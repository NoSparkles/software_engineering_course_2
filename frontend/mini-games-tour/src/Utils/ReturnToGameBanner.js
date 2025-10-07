import { useEffect, useState, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr';
import { useCountdownTimer } from './useCountdownTimer';
import './styles.css';

export function ReturnToGameBanner() {
  const navigate = useNavigate();
  const location = useLocation();
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const [forceTimerReset, setForceTimerReset] = useState(0);
  const [roomCloseTime, setRoomCloseTime] = useState(() => localStorage.getItem("roomCloseTime"));
  const [playerLeft, setPlayerLeft] = useState(false);
  const [shouldShowTimer, setShouldShowTimer] = useState(false);
  const signalrRef = useRef([]);
  const timeLeft = useCountdownTimer(forceTimerReset);

  // --- Banner visibility logic ---
  useEffect(() => {
    async function checkBanner() {
      // Never show banner if declinedReconnectionFlag is set and no session
      if (localStorage.getItem("declinedReconnectionFlag") === "1" && !localStorage.getItem("activeGame")) {
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        setPlayerLeft(false);
        return;
      }

      // On login, always check backend for active session and show banner if needed
      if (localStorage.getItem("token")) {
        try {
          const playerId = localStorage.getItem("playerId");
          const username = localStorage.getItem("username");
          let url = "/api/active-session?";
          if (playerId) url += `playerId=${encodeURIComponent(playerId)}&`;
          if (username) url += `username=${encodeURIComponent(username)}`;
          const resp = await fetch(url);
          if (resp.ok) {
            const data = await resp.json();
            if (data && data.activeGame && data.roomCloseTime) {
              const info = data.activeGame;
              const closeTime = data.roomCloseTime;
              localStorage.setItem("activeGame", JSON.stringify(info));
              localStorage.setItem("roomCloseTime", closeTime);
              sessionStorage.setItem("lastActiveGame", JSON.stringify(info));
              sessionStorage.setItem("lastRoomCloseTime", closeTime);
              window.__lastActiveGame = JSON.stringify(info);
              window.__lastRoomCloseTime = closeTime;

              // Only show banner if not in active room
              const activePaths = [
                `/${info.gameType}/session/${info.code}`,
                `/${info.gameType}/waiting/${info.code}`,
                `/${info.gameType}/matchmaking-session/${info.code}`,
                `/${info.gameType}/matchmaking-waiting/${info.code}`
              ];
              const currentPath = window.location.pathname;
              const isInActiveRoom = activePaths.some(p => currentPath.startsWith(p));
              if (!isInActiveRoom) {
                setShowBanner(true);
                setGameInfo(info);
                setRoomCloseTime(closeTime);
                setShouldShowTimer(!!closeTime);
                setPlayerLeft(!!closeTime);
                return;
              }
            }
          }
        } catch {
          // fallback to localStorage logic below
        }
      }

      let session = localStorage.getItem("activeGame");
      let closeTime = localStorage.getItem("roomCloseTime");

      // Restore session/closeTime from sessionStorage if missing
      if (!session || !closeTime) {
        const lastSession = sessionStorage.getItem("lastActiveGame");
        const lastCloseTime = sessionStorage.getItem("lastRoomCloseTime");
        if (lastSession && lastCloseTime) {
          session = lastSession;
          closeTime = lastCloseTime;
          localStorage.setItem("activeGame", session);
          localStorage.setItem("roomCloseTime", lastCloseTime);
        }
      }

      // Restore from history.state (SPA navigation)
      if ((!session || !closeTime) && window.history.state && window.history.state.activeGame && window.history.state.roomCloseTime) {
        session = JSON.stringify(window.history.state.activeGame);
        closeTime = window.history.state.roomCloseTime;
        localStorage.setItem("activeGame", session);
        localStorage.setItem("roomCloseTime", closeTime);
        sessionStorage.setItem("lastActiveGame", session);
        sessionStorage.setItem("lastRoomCloseTime", closeTime);
      }

      // Restore from global variable (edge SPA navigation)
      if ((!session || !closeTime) && window.__lastActiveGame && window.__lastRoomCloseTime) {
        session = window.__lastActiveGame;
        closeTime = window.__lastRoomCloseTime;
        localStorage.setItem("activeGame", session);
        localStorage.setItem("roomCloseTime", closeTime);
        sessionStorage.setItem("lastActiveGame", session);
        sessionStorage.setItem("lastRoomCloseTime", closeTime);
      }

      // Always check backend for active session using both playerId and username
      let backendSession = null;
      let backendCloseTime = null;
      try {
        const playerId = localStorage.getItem("playerId");
        const username = localStorage.getItem("username");
        let url = "/api/active-session?";
        if (playerId) url += `playerId=${encodeURIComponent(playerId)}&`;
        if (username) url += `username=${encodeURIComponent(username)}`;
        const resp = await fetch(url);
        if (resp.ok) {
          const data = await resp.json();
          if (data && data.activeGame && data.roomCloseTime) {
            backendSession = JSON.stringify(data.activeGame);
            backendCloseTime = data.roomCloseTime;
            localStorage.setItem("activeGame", backendSession);
            localStorage.setItem("roomCloseTime", backendCloseTime);
            sessionStorage.setItem("lastActiveGame", backendSession);
            sessionStorage.setItem("lastRoomCloseTime", backendCloseTime);
            window.__lastActiveGame = backendSession;
            window.__lastRoomCloseTime = backendCloseTime;
          } else {
            localStorage.removeItem("activeGame");
            localStorage.removeItem("roomCloseTime");
            sessionStorage.removeItem("lastActiveGame");
            sessionStorage.removeItem("lastRoomCloseTime");
            setShowBanner(false);
            setGameInfo(null);
            setRoomCloseTime(null);
            setShouldShowTimer(false);
            setPlayerLeft(false);
            return;
          }
        }
      } catch {
        backendSession = localStorage.getItem("activeGame");
        backendCloseTime = localStorage.getItem("roomCloseTime");
      }

      session = backendSession || session;
      closeTime = backendCloseTime || closeTime;

      // If session/closeTime still missing, hide banner
      if (!session || !closeTime) {
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        setPlayerLeft(false);
        return;
      }

      // Only show banner if not in active room, and only show timer if player has left
      let show = false;
      let info = null;
      let timerShouldShow = false;
      let playerLeftFlag = false;
      if (session && closeTime) {
        info = JSON.parse(session);
        const activePaths = [
          `/${info.gameType}/session/${info.code}`,
          `/${info.gameType}/waiting/${info.code}`,
          `/${info.gameType}/matchmaking-session/${info.code}`,
          `/${info.gameType}/matchmaking-waiting/${info.code}`
        ];
        const currentPath = location.pathname;
        const isInActiveRoom = activePaths.some(p => currentPath.startsWith(p));
        show = !isInActiveRoom;

        playerLeftFlag = !!closeTime && show;
        timerShouldShow = playerLeftFlag;
      }
      setShowBanner(show);
      setGameInfo(info);
      setRoomCloseTime(closeTime || null);
      setShouldShowTimer(timerShouldShow);
      setPlayerLeft(playerLeftFlag);
    }

    checkBanner();

    // Listen for navigation, reload, storage, focus, popstate, visibilitychange, custom events
    const handleStorage = () => checkBanner();
    const handleFocus = () => checkBanner();
    const handleCustomEvent = (event) => {
      if (event && event.detail) {
        const { gameData, roomCloseTime } = event.detail;
        if (gameData && roomCloseTime) {
          localStorage.setItem("activeGame", JSON.stringify(gameData));
          localStorage.setItem("roomCloseTime", roomCloseTime);
          checkBanner();
        }
      }
    };
    const handlePopState = () => checkBanner();
    const handleVisibility = () => checkBanner();
    const handleLeaveRoomBanner = () => checkBanner();

    window.addEventListener("storage", handleStorage);
    window.addEventListener("focus", handleFocus);
    window.addEventListener("SetReturnBannerData", handleCustomEvent);
    window.addEventListener("popstate", handlePopState);
    window.addEventListener("visibilitychange", handleVisibility);
    window.addEventListener("LeaveRoomBannerCheck", handleLeaveRoomBanner);

    return () => {
      window.removeEventListener("storage", handleStorage);
      window.removeEventListener("focus", handleFocus);
      window.removeEventListener("SetReturnBannerData", handleCustomEvent);
      window.removeEventListener("popstate", handlePopState);
      window.removeEventListener("visibilitychange", handleVisibility);
      window.removeEventListener("LeaveRoomBannerCheck", handleLeaveRoomBanner);
    };
  }, [location]);

  // Listen for login/logout events and update banner, and auto-navigate to home on logout
  useEffect(() => {
    function handleAuthChange(e) {
      // On logout, remove session and hide banner, navigate to home
      if (e.key === "token" && e.newValue === null) {
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        setPlayerLeft(false);
        navigate('/');
      }
      // On login, check for active session and show banner if needed
      if (e.key === "token" && e.newValue) {
        setTimeout(() => {
          const session = localStorage.getItem("activeGame");
          const closeTime = localStorage.getItem("roomCloseTime");
          // Only show banner if not in active room
          if (session && closeTime) {
            const info = JSON.parse(session);
            const activePaths = [
              `/${info.gameType}/session/${info.code}`,
              `/${info.gameType}/waiting/${info.code}`,
              `/${info.gameType}/matchmaking-session/${info.code}`,
              `/${info.gameType}/matchmaking-waiting/${info.code}`
            ];
            const currentPath = window.location.pathname;
            const isInActiveRoom = activePaths.some(p => currentPath.startsWith(p));
            if (!isInActiveRoom) {
              setShowBanner(true);
              setGameInfo(info);
              setRoomCloseTime(closeTime);
            }
          }
        }, 200);
      }
    }
    window.addEventListener("storage", handleAuthChange);
    return () => window.removeEventListener("storage", handleAuthChange);
  }, [navigate]);

  // Timer auto-close logic
  useEffect(() => {
    if (shouldShowTimer && roomCloseTime) {
      const interval = setInterval(() => {
        const now = Date.now();
        const closeTimestamp = Date.parse(roomCloseTime);
        if (closeTimestamp && closeTimestamp <= now) {
          localStorage.removeItem("activeGame");
          localStorage.removeItem("roomCloseTime");
          setShowBanner(false);
          setGameInfo(null);
          setRoomCloseTime(null);
          setPlayerLeft(false);
          setShouldShowTimer(false);
        }
      }, 1000);
      return () => clearInterval(interval);
    }
  }, [shouldShowTimer, roomCloseTime]);

  if (!showBanner || !gameInfo || roomCloseTime === null) return null;

  const handleReturnToGame = () => {
    const path = gameInfo.isMatchmaking
      ? `/${gameInfo.gameType}/matchmaking-session/${gameInfo.code}`
      : `/${gameInfo.gameType}/session/${gameInfo.code}`;
    localStorage.removeItem("roomCloseTime");
    setRoomCloseTime(null);
    setShowBanner(false);
    setShouldShowTimer(false);
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
      localStorage.setItem("declinedReconnectionFlag", "1");
      setRoomCloseTime(null);
      setShowBanner(false);
      setShouldShowTimer(false);
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
        {shouldShowTimer && (
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
      {shouldShowTimer && (
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

// --- PATCH: Export markLeaveByHome and related helpers
export function markLeaveByHome() {
  sessionStorage.setItem("leaveByHome", "1");
}
export function wasLeaveByHome() {
  return sessionStorage.getItem("leaveByHome") === "1";
}
export function clearLeaveByHome() {
  sessionStorage.removeItem("leaveByHome");
}