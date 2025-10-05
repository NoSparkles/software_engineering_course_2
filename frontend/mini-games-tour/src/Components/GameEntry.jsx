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
    console.log("GameEntry: handleJoinMatchmaking called");
    console.log("GameEntry: token exists:", !!token);
    console.log("GameEntry: playerId exists:", !!playerId);
    console.log("GameEntry: gameType:", gameType);
    
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
      });

      connection.on("MatchmakingError", (errorMessage) => {
        setError(`Matchmaking error: ${errorMessage}`);
      });

      connection.on("MatchFound", (roomCode) => {
    console.log("GameEntry: MatchFound event received");
    setError('');
        
    // Clear any existing game session before setting the new one
    localStorage.removeItem("activeGame");
    localStorage.removeItem("roomCloseTime");
        
    const activeGameData = {
      gameType,
      code: roomCode,
      playerId: playerId,
      isMatchmaking: true
    };
    localStorage.setItem("activeGame", JSON.stringify(activeGameData));
    
    console.log("GameEntry: About to navigate to matchmaking waiting room");
    navigate(`/${gameType}/matchmaking-waiting/${roomCode}`);
    console.log("GameEntry: Navigation completed");
  });

      connection.on("WaitingForOpponent", (roomCode) => {
        console.log("GameEntry: WaitingForOpponent event received");
        setError('');

        // Clear any existing game session before setting the new one
        localStorage.removeItem("activeGame");
        localStorage.removeItem("roomCloseTime");

        const activeGameData = {
          gameType,
          code: roomCode,
          playerId: playerId,
          isMatchmaking: true
        };
        localStorage.setItem("activeGame", JSON.stringify(activeGameData));
        navigate(`/${gameType}/matchmaking-waiting/${roomCode}`);
      });

      connection.on("StartGame", (roomCode) => {
        setError('');
        navigate(`/${gameType}/matchmaking-session/${roomCode}`);
      });
      
      // Call the matchmaking method
      console.log("GameEntry: Calling JoinMatchmaking with:", { gameType, playerId });
      await connection.invoke("JoinMatchmaking", token, gameType, playerId);
      console.log("GameEntry: JoinMatchmaking call completed");
      
      // Don't stop the connection here - let the event handlers handle it
      
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
        <h3>Or</h3>
        <button onClick={() => {
          console.log("GameEntry: Matchmaking button clicked!");
          console.log("GameEntry: About to call handleJoinMatchmaking");
          console.log("GameEntry: Current state:", { gameType, playerId, token: !!token });
          try {
            handleJoinMatchmaking();
            console.log("GameEntry: handleJoinMatchmaking call completed");
          } catch (error) {
            console.error("GameEntry: Error in handleJoinMatchmaking:", error);
          }
        }}>Matchmaking</button>
        {error && <p className="error">{error}</p>}
        
      </div>
    </div>
  );
}