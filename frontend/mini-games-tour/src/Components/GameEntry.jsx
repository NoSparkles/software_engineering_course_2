import React, { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import './styles.css';
import { usePlayerId } from '../Utils/usePlayerId';
import {useAuth} from '../Utils/AuthProvider';

export default function GameEntry() {
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const location = useLocation();
  const navigate = useNavigate();
  const playerId = usePlayerId();
  const { token } = useAuth();
  const [isJoiningMatchmaking, setIsJoiningMatchmaking] = useState(false);
  const gameType = location.pathname.split('/')[1]; // rps, 4inarow, matching

  const handleJoinByCode = async () => {
    const trimmedCode = code.trim().toUpperCase();

    if (!trimmedCode) {
      setError('Please enter a code.');
      return;
    }

    const isValid = /^[A-Z0-9]{6}$/.test(trimmedCode);
    if (!isValid) {
      setError('Code must be 6 characters: A-Z, 0-9.');
      return;
    }

    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/JoinByCodeHub") // fixed casing
        .build();

      await connection.start();
      const exists = await connection.invoke("RoomExists", gameType, trimmedCode);
      await connection.stop();

      if (!exists) {
        setError("Room does not exist or has expired.");
        return;
      }

      setError('');
      navigate(`/${gameType}/waiting/${trimmedCode}`);
    } catch (err) {
      console.error("Error checking room:", err);
      setError("Could not verify room. Try again.");
    }
  };

  const handleJoinAsSpectator = async () => {
    const trimmedCode = code.trim().toUpperCase();

    if (!trimmedCode) {
      setError('Please enter a code.');
      return;
    }

    const isValid = /^[A-Z0-9]{6}$/.test(trimmedCode);
    if (!isValid) {
      setError('Code must be 6 characters: A-Z, 0-9.');
      return;
    }

    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/JoinByCodeHub") // fixed casing
        .build();

      await connection.start();
      const exists = await connection.invoke("RoomExists", gameType, trimmedCode);
      await connection.stop();

      if (!exists) {
        setError("Room does not exist or has expired.");
        return;
      }

      setError('');
      // navigate directly to the session as a spectator
      navigate(`/${gameType}/session/${trimmedCode}?spectator=true`);
    } catch (err) {
      console.error("Error checking room for spectator:", err);
      setError("Could not verify room. Try again.");
    }
  };

  const handleStartMatchmaking = async (gameType) => {
    if (isJoiningMatchmaking) {
      return;
    }

    setIsJoiningMatchmaking(true);

    // Get the current active game before clearing it
    const currentActiveGame = localStorage.getItem("activeGame");
    if (currentActiveGame) {
      try {
        const gameData = JSON.parse(currentActiveGame);
        // Set flag to prevent old room component from navigating on RoomClosed
        sessionStorage.setItem(`hasLeftRoom_${gameData.code}`, "1");
        console.log(`Set hasLeftRoom flag for old room ${gameData.code}`);
      } catch (e) {}
    }

    localStorage.removeItem("activeGame");
    localStorage.removeItem("roomCloseTime");

    if (!token) {
      setError("You must be logged in to join matchmaking.");
      setIsJoiningMatchmaking(false);
      return;
    }

    try {
      const connection = new HubConnectionBuilder()
        .withUrl(`http://localhost:5236/MatchMakingHub?playerId=${playerId}&gameType=${gameType}&roomCode=matchmaking&token=${token}`)
        .build();

      await connection.start();

      const roomCode = await connection.invoke("JoinMatchmaking", token, gameType, playerId);

      if (roomCode) {
        await connection.stop();
        navigate(`/${gameType}/matchmaking-session/${roomCode}`);
      } else {
        await connection.stop();
        setError("Failed to join matchmaking. Please try again.");
        setIsJoiningMatchmaking(false);
      }
    } catch (err) {
      console.error("Error in handleStartMatchmaking:", err);
      setError("Could not join matchmaking. Try again.");
      setIsJoiningMatchmaking(false);
    }
  };

  const handleCreateRoom = async () => {
    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/JoinByCodeHub") // fixed casing
        .build();

      await connection.start();
      const roomCode = await connection.invoke("CreateRoom",  gameType, false);
      await connection.stop();

      if (!roomCode) {
        setError("Failed to create room. Try again.");
        return;
      }

      setError('');
      navigate(`/${gameType}/waiting/${roomCode}`);
    } catch (err) {
      console.error("Error creating room:", err);
      setError("Could not create room. Try again.");
    }
  };

  return (
    <div className="game-entry-container">
      <h2>{gameType.toUpperCase()} Game</h2>

      <div className="entry-section">
        <button onClick={handleCreateRoom}>Create Room</button>
        <h3>Or</h3>
        <h3>Join by Code</h3>
        <input
          className='room-code'
          type="text"
          value={code}
          onChange={e => setCode(e.target.value.toUpperCase())}
          placeholder="Enter game code"
          maxLength={6}
        />
        <div style={{ display: 'flex', gap: 8, justifyContent: 'center', alignItems: 'center', marginTop: 12 }}>
          <button onClick={handleJoinByCode}>Join</button>
          <button onClick={handleJoinAsSpectator}>Join as Spectator</button>
        </div>
        {error && <p className="error">{error}</p>}
        <h3>Or</h3>
        <button 
          onClick={() => handleStartMatchmaking(gameType)}
          disabled={isJoiningMatchmaking}
          style={{ opacity: isJoiningMatchmaking ? 0.5 : 1 }}
        >
          {isJoiningMatchmaking ? 'Joining...' : 'Matchmaking'}
        </button>
        {error && <p className="error">{error}</p>}
        
      </div>
    </div>
  );
}