import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useSignalRService } from '../../Utils/useSignalRService';
import { usePlayerId } from '../../Utils/usePlayerId';
import { useAuth } from '../../Utils/AuthProvider';

export default function SpectatePage() {
  const { gameType, code } = useParams();
  const navigate = useNavigate();
  const playerId = usePlayerId();
  const { token } = useAuth();
  const [status, setStatus] = useState('Connecting to spectator stream...');

  useEffect(() => {
    navigate(`/${gameType}/session/${code}?spectator=true`);
  }, [navigate, gameType, code]);

  return (
    <div style={{ padding: 20 }}>
      <h2>Spectate {gameType}</h2>
      <p>Room code: <strong>{code}</strong></p>
  <p>{status}</p>
    </div>
  );
}
