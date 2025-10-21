import React, { useEffect, useState } from 'react';
import { connectSpectator, joinSpectate, leaveSpectate, disconnectSpectator } from '../../Services/spectatorService';

export default function SpectatePage({ params }) {
  // react-router v6 uses useParams, but this component will read from URL to be safe
  const query = new URLSearchParams(window.location.search);
  const gameType = params?.gameType || window.location.pathname.split('/')[1];
  const code = params?.code || window.location.pathname.split('/').pop();

  const [gameState, setGameState] = useState(null);
  const [spectators, setSpectators] = useState([]);
  const [status, setStatus] = useState('connecting');
  const [error, setError] = useState(null);

  useEffect(() => {
    const spectatorId = `spec-${Math.random().toString(36).slice(2,8)}`;
    const username = `Spectator-${spectatorId.slice(-4)}`;
    const backendHost = window.__BACKEND_HOST__ || 'http://localhost:5236';
    const hubUrl = `${backendHost}/JoinByCodeHub`;

    let timedOut = false;
    const timeout = setTimeout(() => {
      timedOut = true;
      setStatus('error');
      setError('Connection timed out');
    }, 6000);

    connectSpectator(hubUrl,
      (state) => setGameState(state),
      (spec) => setSpectators(prev => [...prev, spec]),
      (id) => setSpectators(prev => prev.filter(s => s.id !== id))
    ).then((conn) => {
      if (timedOut) return; // ignore late success
      clearTimeout(timeout);
      setStatus('connected');
      // attach an onclose to update UI if connection drops
      if (conn && conn.onclose) {
        conn.onclose(() => {
          setStatus('error');
          setError('Connection closed');
        });
      }
      // Use JoinByCodeHub's JoinAsSpectator so server will send the initial game state
      conn.invoke('JoinAsSpectator', gameType, code)
        .then(() => console.log('JoinAsSpectator succeeded', { gameType, code, spectatorId, username }))
        .catch(err => {
          console.error('JoinAsSpectator failed:', err);
          setError(err?.message || String(err));
          setStatus('error');
        });
    }).catch(err => {
      if (timedOut) return;
      clearTimeout(timeout);
      console.error(err);
      setError(err?.message || String(err));
      setStatus('error');
    });

    return () => {
      leaveSpectate(gameType, code, spectatorId).catch(()=>{});
      disconnectSpectator();
      // clear spectator activeGame marker when leaving
      try { const ag = JSON.parse(localStorage.getItem('activeGame')||'null'); if (ag && ag.code === code && ag.isSpectator) { localStorage.removeItem('activeGame'); } } catch(e){}
    };
  }, [gameType, code]);

  return (
    <div>
      <h2>Spectating {gameType} room {code}</h2>
      <div>
        <h3>Spectators</h3>
        <ul>{spectators.map(s => <li key={s.id}>{s.username || s.id}</li>)}</ul>
      </div>
      <div>
        <h3>Game state</h3>
        <pre>{JSON.stringify(gameState, null, 2)}</pre>
      </div>
      <div>
        <strong>Status:</strong> {status}
        {error && <div style={{ color: 'red' }}>Error: {error}</div>}
      </div>
    </div>
  );
}
