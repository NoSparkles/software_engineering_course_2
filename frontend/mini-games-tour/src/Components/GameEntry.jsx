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
        .withUrl("http://localhost:5236/joinByCodeHub")
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
        .withUrl("http://localhost:5236/JoinByCodeHub")
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

  const handleJoinMatchmaking = async () => {
    if (!token) {
      setError('Please log in to use matchmaking.');
      return;
    }

    if (!playerId) {
      setError('Player ID not ready. Please try again.');
      return;
    }

    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/MatchMakingHub", {
          accessTokenFactory: () => token 
        })
        .withAutomaticReconnect()
        .build();
      
      await connection.start();
      
      // Set up event listeners for matchmaking responses
      connection.on("UnauthorizedMatchmaking", () => {
        setError("Authentication failed. Please log in again.");
        connection.stop();
      });

      connection.on("MatchmakingError", (errorMessage) => {
        setError(`Matchmaking error: ${errorMessage}`);
        connection.stop();
      });

      connection.on("MatchFound", (roomCode) => {
        setError('');
        connection.stop();
        navigate(`/${gameType}/matchmaking-waiting/${roomCode}`);
      });

      connection.on("WaitingForOpponent", (roomCode) => {
        setError('');
        connection.stop();
        navigate(`/${gameType}/matchmaking-waiting/${roomCode}`);
      });

      connection.on("StartGame", (roomCode) => {
        setError('');
        connection.stop();
        navigate(`/${gameType}/matchmaking-session/${roomCode}`);
      });
      
      // Call the matchmaking method
      console.log("Calling JoinMatchmaking with:", { token: token ? "present" : "missing", gameType, playerId });
      await connection.invoke("JoinMatchmaking", token, gameType, playerId);
      
    } catch (err) {
      console.error("Error with matchmaking:", err);
      setError("Could not start matchmaking. Please try again.");
    }
};

  const handleCreateRoom = async () => {
    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/joinByCodeHub")
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
      </div>

      <div className="entry-section">
        <h3>Or</h3>
        <button onClick={handleJoinMatchmaking}>Matchmaking</button>
        {error && <p className="error">{error}</p>}
      </div>
    </div>
  );
}