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

    const { gameType, code } = JSON.parse(session);
    const currentPath = location.pathname;
    const targetPath = `/${gameType}/session/${code}`;

    if (currentPath === targetPath) {
      // If already in the game, hide the banner
      setShowBanner(false);
      return;
    }

    const checkRoom = async () => {
      try {
        const connection = new HubConnectionBuilder()
          .withUrl("http://localhost:5236/gamehub")
          .build();

        await connection.start();
        const exists = await connection.invoke("RoomExists", gameType, code);
        await connection.stop();
        console.log("checking if exists: " + exists);

        if (exists) {
          setGameInfo({ gameType, code });
          setShowBanner(true);
        } else {
          localStorage.removeItem("activeGame");
          setShowBanner(false);
        }
      } catch (err) {
        console.error("Room check failed:", err);
      }
    };

    checkRoom();
  }, [location]);

  if (!showBanner || !gameInfo) return null;

  return (
    <div className="return-banner">
      <p>You have an active game session.</p>
      <button onClick={() => navigate(`/${gameInfo.gameType}/session/${gameInfo.code}`)}>
        Return to Game
      </button>
    </div>
  );
}
