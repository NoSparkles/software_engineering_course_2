import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useCountdownTimer } from '../../Utils/useCountdownTimer';
import PMBoard from '../../Games/PairMatchingGame/Components/GameBoard';
import RpsBoard from '../../Games/RockPaperScissors/Components/RpsBoard';
import {Board as FourInARowGameBoard} from '../../Games/FourInRowGame/Components/Board';
import { useAuth } from '../../Utils/AuthProvider';
import { globalConnectionManager } from '../../Utils/GlobalConnectionManager';
import './styles.css';
import { markLeaveByHome } from '../../Utils/ReturnToGameBanner';

export default function SessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const { user, token } = useAuth()
  const query = new URLSearchParams(window.location.search);
  const isSpectator = query.get('spectator') === 'true';
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  const [playerColor, setPlayerColor] = useState(null);
  const playerId = usePlayerId();
  const timeLeft = useCountdownTimer();
  
  const { connection, connectionState, reconnected } = useSignalRService({
    hubUrl: "http://localhost:5236/joinByCodeHub",
    gameType,
    roomCode: code,
    playerId,
    token,
  });

  useEffect(() => {
      const activeGameData = {
        gameType,
        code: code,
        playerId: playerId,
        isMatchmaking: false
      };
      localStorage.setItem("activeGame", JSON.stringify(activeGameData));
  }, [code, gameType, playerId]);

  useEffect(() => {
    if (connection) {
      globalConnectionManager.registerConnection('sessionRoom', connection, {
        gameType,
        roomCode: code,
        playerId
      });
      
      return () => {
        globalConnectionManager.unregisterConnection('sessionRoom');
      };
    }
  }, [connection, connectionState, gameType, code, playerId]);

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      switch (gameType) {
          case 'rock-paper-scissors':
            setBoard(<RpsBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
          case 'four-in-a-row':
            setBoard(<FourInARowGameBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
          case 'pair-matching':
            setBoard(<PMBoard playerColor={playerColor} connection={connection} connectionState={connectionState} roomCode={code} playerId={playerId} spectator={isSpectator} token={token}/>);
            break;
              default:
                  setBoard(null);
        }
    } else {
      setBoard(null);
    }
  }, [code, connection, connectionState, gameType, isSpectator, playerColor, playerId, token])

  useEffect(() => {
    if (connection && connectionState === "Connected") {
      if (isSpectator) {
        connection.invoke("JoinAsSpectator", gameType, code)
          .then(() => setStatus("Joined as spectator"))
          .catch(err => {
            console.error("JoinAsSpectator failed:", err);
            setStatus("Failed to join as spectator.");
          });
      } else {
        connection.invoke("Join", gameType, code, playerId, token)
          .then(() => setStatus("Joined room. Waiting for opponent..."))
          .catch(err => {
            console.error("Join failed:", err);
            setStatus("Failed to join room.");
          });
      }

      connection.on("WaitingForOpponent", () => {
        setStatus("Waiting for second player...");
      });

      connection.on("StartGame", (roomCode) => {
        if (roomCode === code) {
          setStatus("Game started! Good luck!");
          localStorage.removeItem("roomCloseTime");
          setGameStarted(true);
          setRoomCloseTime(null);
        }
      });

      connection.on("PlayerLeft", (leftPlayerId, message, roomCloseTime) => {
        setStatus(message);
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
          setRoomCloseTime(roomCloseTime);
        } else {
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
          setRoomCloseTime(fallbackCloseTime);
        }
      });

      connection.on("PlayerLeftRoom", (message, roomCloseTime) => {
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
        } else {
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
        }
      });

      connection.on("Reconnected", () => {
        setStatus("Reconnected to room.");
        if (connection && connectionState === "Connected" && !isSpectator) {
          connection.invoke("Join", gameType, code, playerId, token)
            .then(() => setStatus("Rejoined room"))
            .catch(err => console.error("Rejoin failed:", err));
        }
      });

      connection.on("SetPlayerColor", (color) => {
        setPlayerColor(color[playerId]);
      });

      connection.on("SpectatorJoined", (roomCode) => {
        setStatus("Spectating room " + roomCode);
      });

      connection.on("SpectatorJoinFailed", (msg) => {
        setStatus("Spectator join failed: " + msg);
      });

      connection.on("PlayerDisconnected", (disconnectedPlayerId, message, roomCloseTime) => {
        setStatus(message);
        if (roomCloseTime) {
          localStorage.setItem("roomCloseTime", roomCloseTime);
          setRoomCloseTime(roomCloseTime);
        } else {
          const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
          localStorage.setItem("roomCloseTime", fallbackCloseTime);
          setRoomCloseTime(fallbackCloseTime);
        }
      });

      connection.on("PlayerReconnected", (reconnectedPlayerId, message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        setRoomCloseTime(null);
      });

      connection.on("RoomClosing", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        setTimeout(() => {
          navigate('/');
        });
      });

      connection.on("RoomClosed", (message) => {
        setStatus(message);
        localStorage.removeItem("roomCloseTime");
        localStorage.removeItem("activeGame");
        setTimeout(() => {
          navigate('/');
        }, 2000);
      });

      return () => {
        if (!isSpectator && connection && connection.state === "Connected") {
          console.log("Component unmounting - calling LeaveRoom...");
          connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
            console.warn("LeaveRoom failed on unmount:", err);
          });
          console.log("LeaveRoom call initiated on unmount");
        }
        
        connection.off("WaitingForOpponent");
        connection.off("StartGame");
        connection.off("PlayerLeft");
        connection.off("PlayerLeftRoom");
        connection.off("Reconnected");
        connection.off("SetPlayerColor");
        connection.off("SpectatorJoined");
        connection.off("SpectatorJoinFailed");
        connection.off("PlayerDisconnected");
        connection.off("PlayerReconnected");
        connection.off("RoomClosing");
        connection.off("RoomClosed");
      };
    }
  }, [gameType, code, navigate, connection, connectionState, playerId, token]);

  useEffect(() => {
    const handleBeforeUnload = () => {
      console.log("beforeunload event triggered");
      if (!isSpectator && connection && connection.state === "Connected") {
        console.log("Calling LeaveRoom on beforeunload...");
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("LeaveRoom failed on beforeunload:", err);
        });
        console.log("LeaveRoom call initiated on beforeunload");
      }
    };

    const handlePopState = () => {
      console.log("popstate event triggered");
      if (!isSpectator && connection && connection.state === "Connected") {
        console.log("Calling LeaveRoom on popstate...");
        connection.invoke("LeaveRoom", gameType, code, playerId).catch(err => {
          console.warn("LeaveRoom failed on popstate:", err);
        });
        console.log("LeaveRoom call initiated on popstate");
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    window.addEventListener('popstate', handlePopState);
    
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
      window.removeEventListener('popstate', handlePopState);
    };
  }, [connection, isSpectator]);

  const handleLeaveRoom = async () => {
    if (!isSpectator && connection && connection.state === "Connected") {
      try {
        await connection.invoke("LeaveRoom", gameType, code, playerId);
        window.dispatchEvent(new Event("LeaveRoomBannerCheck"));
      } catch (err) {
        console.warn("LeaveRoom failed:", err);
      }
    }
    markLeaveByHome();
    setTimeout(() => {
      const activeGameData = {
        gameType,
        code: code,
        playerId: playerId,
        isMatchmaking: false
      };
      localStorage.setItem("activeGame", JSON.stringify(activeGameData));
      sessionStorage.setItem("lastActiveGame", JSON.stringify(activeGameData));
      if (!localStorage.getItem("roomCloseTime")) {
        const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
        localStorage.setItem("roomCloseTime", fallbackCloseTime);
        sessionStorage.setItem("lastRoomCloseTime", fallbackCloseTime);
      } else {
        sessionStorage.setItem("lastRoomCloseTime", localStorage.getItem("roomCloseTime"));
      }
      window.dispatchEvent(new Event("LeaveRoomBannerCheck"));
    }, 300);
    navigate('/');
  };

  const [roomCloseTime, setRoomCloseTime] = useState(() => localStorage.getItem("roomCloseTime"));
  const [roomPlayers, setRoomPlayers] = useState([playerId]);
  const [gameStarted, setGameStarted] = useState(false);

  useEffect(() => {
    function handleRoomCloseTimeChange() {
      setRoomCloseTime(localStorage.getItem("roomCloseTime"));
    }
    window.addEventListener("storage", handleRoomCloseTimeChange);

    if (connection) {
      connection.on("RoomPlayersUpdate", (players) => {
        setRoomPlayers(players);
      });

      connection.on("PlayerDeclinedReconnection", (declinedPlayerId, message) => {
        const fallbackCloseTime = new Date(Date.now() + 30000).toISOString();
        setRoomCloseTime(fallbackCloseTime);
        localStorage.setItem("roomCloseTime", fallbackCloseTime);
      });
      return () => {
        window.removeEventListener("storage", handleRoomCloseTimeChange);
        connection.off("RoomPlayersUpdate");
        connection.off("PlayerDeclinedReconnection");
      };
    }
    return () => window.removeEventListener("storage", handleRoomCloseTimeChange);
  }, [connection]);

  const showTimer = !isSpectator &&
    roomCloseTime &&
    Date.parse(roomCloseTime) > Date.now() &&
    timeLeft !== null &&
    timeLeft > 0 &&
    (!gameStarted || roomPlayers.length < 2);

  return (
    <div className="session-room">
      <h2>{gameType.toUpperCase()} Session</h2>
      <p>Room Code: <strong>{code}</strong></p>
      <p>
        Assigned Color: <strong>{playerColor ? (playerColor === "R" ? "Red" : "Yellow") : "Not assigned yet"}</strong>
      </p>

      {connectionState === "Disconnected" && (
        <button className="reconnect-btn" onClick={() => {
          if (connection) connection.start().catch(err => console.error("Reconnection failed:", err));
        }}>
          Reconnect
        </button>
      )}

      <div style={{ marginTop: '20px', textAlign: 'center' }}>
        <button onClick={handleLeaveRoom}>
          🚪 Leave Room
        </button>
      </div>

      {showTimer ? (
        <div className={`time-left ${timeLeft <= 10 ? 'short' : timeLeft <= 20 ? 'medium' : 'long'}`}>
          {timeLeft > 0 ? `Room will close in ${timeLeft} seconds` : "Room is closing now!"}
        </div>
      ) : (
        <div className="time-left stable">
          The room will remain open until all players have left.
        </div>
      )}

      <div className="game-board">
        {board}
      </div>
    </div>
  );
}