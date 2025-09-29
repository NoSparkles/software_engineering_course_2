import { useEffect, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import './styles.css';

export default function ReturnToGameBanner() {
  const [showBanner, setShowBanner] = useState(false);
  const [gameInfo, setGameInfo] = useState(null);
  const navigate = useNavigate();
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

  return (
    <div className="return-banner">
      <p>You have an active game session.</p>
      <button onClick={handleReturnToGame}>
        Return to Game
      </button>
    </div>
  );
}
