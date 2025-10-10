import { useEffect, useState, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useCountdownTimer } from './useCountdownTimer';
import './styles.css';

export function ReturnToGameBanner() {
  const navigate = useNavigate();
  const location = useLocation();
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const [roomCloseTime, setRoomCloseTime] = useState(null);
  const [shouldShowTimer, setShouldShowTimer] = useState(false);
  const signalrRef = useRef([]);
  const timeLeft = useCountdownTimer(0);

  // Main banner logic
  useEffect(() => {
    async function checkBanner() {
      // Check if user is logged in
      const token = localStorage.getItem('token');
      if (!token) {
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        return;
      }

      // Check for declined reconnection
      if (localStorage.getItem("declinedReconnectionFlag") === "1" && !localStorage.getItem("activeGame")) {
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        return;
      }

      // Get current session from localStorage first
      let session = localStorage.getItem("activeGame");
      let closeTime = localStorage.getItem("roomCloseTime");

      // If no local session, check backend
      if (!session || !closeTime) {
        try {
          const username = localStorage.getItem("username");
          const playerId = localStorage.getItem("playerId");
          let url = "http://localhost:5236/api/active-session?";
          if (username) url += `username=${encodeURIComponent(username)}&`;
          if (playerId) url += `playerId=${encodeURIComponent(playerId)}`;
          
          const resp = await fetch(url);
          if (resp.ok) {
            const data = await resp.json();
            if (data && data.activeGame && data.roomCloseTime) {
              session = JSON.stringify(data.activeGame);
              closeTime = data.roomCloseTime;
              localStorage.setItem("activeGame", session);
              localStorage.setItem("roomCloseTime", closeTime);
            }
          }
        } catch (error) {
          console.log("Failed to check active session:", error);
        }
      }

      // If still no session, hide banner
      if (!session || !closeTime) {
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
        return;
      }

      // Parse session and determine visibility
      const info = JSON.parse(session);
      const activePaths = [
        `/${info.gameType}/session/${info.code}`,
        `/${info.gameType}/waiting/${info.code}`,
        `/${info.gameType}/matchmaking-session/${info.code}`,
        `/${info.gameType}/matchmaking-waiting/${info.code}`
      ];
      const currentPath = location.pathname;
      const isInActiveRoom = activePaths.some(p => currentPath.startsWith(p));
      const show = !isInActiveRoom;

      console.log("[Banner] Check:", { currentPath, isInActiveRoom, closeTime, info });

      // PATCH: Only show timer if roomCloseTime is in the future AND user is NOT in the room
      let timerShouldShow = false;
      if (
        show &&
        closeTime &&
        Date.parse(closeTime) > Date.now()
      ) {
        timerShouldShow = true;
      } else {
        timerShouldShow = false;
      }

      // PATCH: If user is in the room (session/matchmaking-session), never show timer/banner
      if (isInActiveRoom) {
        console.log("[Banner] User in active room, hiding banner");
        setShowBanner(false);
        setShouldShowTimer(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        return;
      }

      console.log("[Banner] Setting banner state:", { showBanner: timerShouldShow, closeTime });
      setShowBanner(timerShouldShow);
      setGameInfo(info);
      setRoomCloseTime(timerShouldShow ? closeTime : null);
      setShouldShowTimer(timerShouldShow);
    }

    checkBanner();
    
    // Check if we just navigated away (set by Navbar)
    if (sessionStorage.getItem("justNavigatedAway") === "1") {
      sessionStorage.removeItem("justNavigatedAway");
      // Delay to ensure navigation has completed
      setTimeout(() => {
        console.log("[Banner] Checking after navigation delay");
        checkBanner();
      }, 150);
    }
    
    // Also check when localStorage changes
    const handleStorageChange = (e) => {
      if (e.key === "roomCloseTime" || e.key === "activeGame") {
        checkBanner();
      }
    };
    
    // Listen for custom event when localStorage is updated in same window
    const handleLocalStorageUpdate = () => {
      setTimeout(() => checkBanner(), 100);
    };
    
    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("localStorageUpdate", handleLocalStorageUpdate);
    
    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("localStorageUpdate", handleLocalStorageUpdate);
    };
  }, [location]);

  // Set up SignalR connections for real-time updates
  useEffect(() => {
    const session = localStorage.getItem("activeGame");
    if (!session) return;

    const hubUrls = [
      "http://localhost:5236/MatchMakingHub",
      "http://localhost:5236/joinByCodeHub"
    ];

    const connections = [];
    hubUrls.forEach(hubUrl => {
      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

      connection.on("PlayerLeft", () => {
        // Player left, check banner again
        setTimeout(() => {
          const session = localStorage.getItem("activeGame");
          if (session) {
            const info = JSON.parse(session);
            const activePaths = [
              `/${info.gameType}/session/${info.code}`,
              `/${info.gameType}/waiting/${info.code}`,
              `/${info.gameType}/matchmaking-session/${info.code}`,
              `/${info.gameType}/matchmaking-waiting/${info.code}`
            ];
            const currentPath = location.pathname;
            const isInActiveRoom = activePaths.some(p => currentPath.startsWith(p));
            if (!isInActiveRoom) {
              setShowBanner(true);
              setGameInfo(info);
              setRoomCloseTime(localStorage.getItem("roomCloseTime"));
              setShouldShowTimer(true);
            }
          }
        }, 100);
      });

      connection.on("PlayerReconnected", () => {
        // Player reconnected, hide banner and timer
        setShowBanner(false);
        setShouldShowTimer(false);
        setGameInfo(null);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
      });

      connection.on("StartGame", () => {
        // Game started with both players, hide banner and timer
        setShowBanner(false);
        setShouldShowTimer(false);
        setGameInfo(null);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setRoomCloseTime(null);
      });

      connection.on("SetReturnBannerData", function(gameData, roomCloseTime) {
        if (gameData && roomCloseTime) {
          localStorage.setItem("activeGame", JSON.stringify(gameData));
          localStorage.setItem("roomCloseTime", roomCloseTime);
          setGameInfo(gameData);
          setRoomCloseTime(roomCloseTime);
          setShowBanner(true);
          setShouldShowTimer(true);
        }
      });

      connection.on("RoomClosed", function(reason, closedRoomCode) {
        // Clear banner when room is closed
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
      });

      connection.on("SessionDeclined", function(message) {
        // Clear banner when reconnection is declined
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
      });

      connection.start().catch(() => {});
      connections.push(connection);
    });

    signalrRef.current = connections;

    return () => {
      connections.forEach(connection => {
        connection.off("PlayerLeft");
        connection.off("PlayerReconnected");
        connection.off("StartGame");
        connection.off("SetReturnBannerData");
        connection.off("RoomClosed");
        connection.off("SessionDeclined");
        connection.stop();
      });
    };
  }, []);

  // Listen for login/logout events
  useEffect(() => {
    function handleAuthChange(e) {
      // On logout, remove session and hide banner
      if (e.key === "token" && e.newValue === null) {
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");
        setShowBanner(false);
        setGameInfo(null);
        setRoomCloseTime(null);
        setShouldShowTimer(false);
      }
      // On login, check for active session
      if (e.key === "token" && e.newValue) {
        setTimeout(() => {
          const session = localStorage.getItem("activeGame");
          const closeTime = localStorage.getItem("roomCloseTime");
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
              setShouldShowTimer(true);
            }
          }
        }, 200);
      }
    }
    window.addEventListener("storage", handleAuthChange);
    return () => window.removeEventListener("storage", handleAuthChange);
  }, []);

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
          setShouldShowTimer(false);
        }
      }, 1000);
      return () => clearInterval(interval);
    }
  }, [shouldShowTimer, roomCloseTime]);

  if (!showBanner || !gameInfo || roomCloseTime === null) return null;

  const handleReturnToGame = () => {
    const path = `/${gameInfo.gameType}/${gameInfo.isMatchmaking ? 'matchmaking-session' : 'session'}/${gameInfo.code}`;
    setRoomCloseTime(null);
    setShowBanner(false);
    setShouldShowTimer(false);
    navigate(path);
  };

  const handleDeclineReconnection = async () => {
    if (!gameInfo) return;
    
    // Set a flag to prevent old room components from navigating on RoomClosed
    sessionStorage.setItem(`hasLeftRoom_${gameInfo.code}`, "1");
    
    try {
      // Call DeclineReconnection on the appropriate hub
      const hubUrl = gameInfo.isMatchmaking 
        ? "http://localhost:5236/MatchMakingHub" 
        : "http://localhost:5236/joinByCodeHub";
      
      const { HubConnectionBuilder } = await import('@microsoft/signalr');
      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .build();
      
      await connection.start();
      await connection.invoke("DeclineReconnection", gameInfo.playerId, gameInfo.gameType, gameInfo.code);
      await connection.stop();
    } catch (err) {
      console.warn("DeclineReconnection failed:", err);
    }
    
    // Clear local storage
    localStorage.removeItem("activeGame");
    localStorage.removeItem("roomCloseTime");
    localStorage.setItem("declinedReconnectionFlag", "1");
    setRoomCloseTime(null);
    setShowBanner(false);
    setShouldShowTimer(false);
  };

  return (
    <div className="return-to-game-banner">
      <div className="banner-content">
        <p>You have an active game in progress!</p>
        <div className="banner-buttons">
          <button onClick={handleReturnToGame} className="return-button">
            Return to Game
          </button>
          {shouldShowTimer && (
            <button
              onClick={handleDeclineReconnection}
              className="decline-button"
            >
              Decline
            </button>
          )}
        </div>
        {shouldShowTimer && (
          <p style={{
            color: timeLeft <= 10 ? "red" : timeLeft <= 20 ? "orange" : "black",
            fontSize: "14px",
            margin: "5px 0 0 0"
          }}>
            Room will close in {timeLeft} seconds
          </p>
        )}
      </div>
    </div>
  );
}

// Helper functions
export function markLeaveByHome() {
  sessionStorage.setItem("leaveByHome", "1");
}

export function setUsernameLocalStorage(username) {
  if (username) {
    localStorage.setItem("username", username);
    sessionStorage.setItem("username", username);
  }
}

export function setPlayerIdLocalStorage(playerId) {
  if (playerId) {
    localStorage.setItem("playerId", playerId);
    sessionStorage.setItem("playerId", playerId);
  }
}

export default ReturnToGameBanner;