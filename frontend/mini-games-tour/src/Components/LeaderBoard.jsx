import React, { useState, useEffect } from 'react';
import { useAuth } from '../Utils/AuthProvider';
import './styles.css';

export default function LeaderBoard() {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const [players, setPlayers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchLeaderboard = async () => {
      setLoading(true);
      setError(null);

      try {
        const response = await fetch('http://localhost:5236/User');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();

        if (Array.isArray(data)) setPlayers(data);
        else setPlayers([]);
      } catch (err) {
        console.error('Fetch failed:', err);
        setError(`Failed to load leaderboard: ${err.message}`);
        setPlayers([]);
      } finally {
        setLoading(false);
      }
    };

    if (open) fetchLeaderboard();
  }, [open]);

  const handleToggle = () => {
    setOpen(prev => !prev);
    if (open) {
      setPlayers([]);
      setError(null);
    }
  };

  const renderPlayers = () => {
    if (loading) return <p>Loading leaderboard...</p>;
    if (error) return <p style={{ color: 'var(--color-danger)' }}>{error}</p>;
    if (!players.length) return <p>No players found.</p>;

    const sortedPlayers = players
      .map(p => ({
        username: p.username ?? p.Username,
        rps: p.rockPaperScissorsMMR ?? p.RockPaperScissorsMMR ?? 0,
        four: p.fourInARowMMR ?? p.FourInARowMMR ?? 0,
        match: p.pairMatchingMMR ?? p.PairMatchingMMR ?? 0,
      }))
      .map(p => ({ ...p, totalMmr: p.rps + p.four + p.match }))
      .sort((a, b) => b.totalMmr - a.totalMmr);

    // Get top 100
    const top100 = sortedPlayers.slice(0, 100);
    
    // Find current player's rank
    const currentPlayer = user?.username 
      ? sortedPlayers.find(p => p.username === user.username)
      : null;
    const currentPlayerRank = currentPlayer 
      ? sortedPlayers.findIndex(p => p.username === currentPlayer.username) + 1
      : null;

    return (
      <>
        <ul className="leaderboard-panel__list">
          {top100.map((p, i) => (
            <li className="leaderboard-panel__item" key={p.username || i}>
              <div className="leaderboard-panel__row">
                <strong>#{i + 1}</strong>
                <span>{p.username}</span>
                <span>{p.totalMmr} MMR</span>
              </div>
              <div className="leaderboard-panel__meta">
                RPS: {p.rps} • 4-in-Row: {p.four} • Matching: {p.match}
              </div>
            </li>
          ))}
        </ul>
        {currentPlayer && currentPlayerRank && (
          <div style={{ 
            marginTop: '16px', 
            paddingTop: '16px', 
            borderTop: '1px solid rgba(255, 255, 255, 0.1)' 
          }}>
            <div className="leaderboard-panel__item" style={{ 
              background: 'rgba(125, 184, 255, 0.1)',
              border: '1px solid rgba(125, 184, 255, 0.3)'
            }}>
              <div className="leaderboard-panel__row">
                <strong>#{currentPlayerRank}</strong>
                <span>{currentPlayer.username}</span>
                <span>{currentPlayer.totalMmr} MMR</span>
              </div>
              <div className="leaderboard-panel__meta">
                RPS: {currentPlayer.rps} • 4-in-Row: {currentPlayer.four} • Matching: {currentPlayer.match}
              </div>
            </div>
          </div>
        )}
      </>
    );
  };

  return (
    <div className="leaderboard-shell">
      <button className="btn btn--ghost" onClick={handleToggle}>
        {open ? 'Hide Leaderboard' : 'Leaderboard'}
      </button>

      {open && (
        <div className="leaderboard-panel is-visible">
          <div className="leaderboard-panel__header">
            <div>
              <p className="leaderboard-panel__eyebrow">Live standings</p>
              <h4>Top competitors</h4>
            </div>
            <button className="btn btn--ghost" onClick={handleToggle}>
              Close
            </button>
          </div>
          {renderPlayers()}
        </div>
      )}
    </div>
  );
}
