import React, { useState, useEffect } from 'react';
import './styles.css';

export default function LeaderBoard() {
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

    return (
      <ul className="leaderboard-panel__list">
        {sortedPlayers.map((p, i) => (
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
