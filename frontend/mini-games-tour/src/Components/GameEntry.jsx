import React, { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import './styles.css';
import { usePlayerId } from '../Utils/usePlayerId';

export default function GameEntry() {
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const location = useLocation();
  const navigate = useNavigate();
  const playerId = usePlayerId();

  const gameType = location.pathname.split('/')[1]; // e.g. 'rps', '4inarow', 'matching'

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
        .withUrl("http://localhost:5236/gamehub")
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

  const handleCreateRoom = async () => {
    const newCode = Math.random().toString(36).substring(2, 8).toUpperCase();

    try {
      const connection = new HubConnectionBuilder()
        .withUrl("http://localhost:5236/gamehub")
        .build();

      await connection.start();
      const created = await connection.invoke("CreateRoom", gameType, newCode);
      await connection.stop();

      if (!created) {
        setError("Failed to create room. Try again.");
        return;
      }

      setError('');
      navigate(`/${gameType}/waiting/${newCode}`);
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
        <button onClick={handleJoinByCode}>Join</button>
        {error && <p className="error">{error}</p>}
      </div>

      <div className="entry-section">
        <h3>Or</h3>
        <button className="disabled-button">Matchmaking (coming soon)</button>
      </div>
    </div>
  );
}