import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getConnection } from '../../utils/signalRService';
import PMBoard from '../../games/PairMatchingGame/components/GameBoard';
import {Board as FourInARowGameBoard} from '../../games/fourInRowGame/components/Board';

export default function SessionRoom() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState("Game in progress...");
  const [board, setBoard] = useState(null);
  useEffect(() => {
    switch (gameType) {
        case 'rock-paper-scissors':
            setBoard(undefined); // placeholder for Dominykas
            break;
        case 'four-in-a-row':
            setBoard(<FourInARowGameBoard/>);
            break;
        case 'pair-matching':
            setBoard(<PMBoard />);
            break;
        default:
            setBoard(null);
    }
  }, [gameType]);

  useEffect(() => {
    const connection = getConnection();

    if (connection.state === "Disconnected") {
      connection.start()
        .then(() => {
          console.log("Reconnected in session");
          return connection.invoke("JoinRoom", gameType, code);
        })
        .catch(err => {
          console.error("Failed to reconnect:", err);
          setStatus("Connection error.");
        });
    }

    connection.on("PlayerLeft", () => {
      alert("Your opponent has left the game.");
      navigate(`/${gameType}/exit`);
    });

    connection.onclose(() => {
      setStatus("Disconnected.");
    });

    connection.onreconnecting(() => {
      setStatus("Reconnecting...");
    });

    connection.onreconnected(() => {
      setStatus("Reconnected.");
    });

    return () => {
      connection.off("PlayerLeft");
      connection.off("ReceiveMove");
      connection.off("onclose");
      connection.off("onreconnecting");
      connection.off("onreconnected");
    };
  }, [gameType, code, navigate]);
  return (
    <div className="session-room">
      <h2>{gameType.toUpperCase()} Session</h2>
      <p>Room Code: <strong>{code}</strong></p>
      <p>Status: <strong>{status}</strong></p>

      <div className="game-board">
        {board}
      </div>
    </div>
  );
}