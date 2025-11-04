import React, { useState, useEffect, useRef } from 'react';
import './styles.css';

export default function LeaderBoard() {
  const [open, setOpen] = useState(false);
  const [players, setPlayers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [popupHeight, setPopupHeight] = useState(0);

  const popupRef = useRef(null);

  // Fetch users when leaderboard is opened
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

  useEffect(() => {
    if (open && popupRef.current) {
      const resizeObserver = new ResizeObserver(() => {
        const rect = popupRef.current.getBoundingClientRect();
        setPopupHeight(rect.height);
      });
      resizeObserver.observe(popupRef.current);

      return () => resizeObserver.disconnect();
    }
  }, [open, players]);

  const handleToggle = () => {
    setOpen(prev => !prev);
    if (open) {
      setPlayers([]);
      setError(null);
      setPopupHeight(0);
    }
  };

  const renderPlayers = () => {
    if (loading) return <p>Loading leaderboard...</p>;
    if (error) return <p style={{ color: 'red' }}>{error}</p>;
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
      <ul>
        {sortedPlayers.map((p, i) => (
          <li key={p.username || i}>
            {i + 1}. {p.username} - {p.totalMmr} MMR
            <div style={{ fontSize: '0.8em', color: '#ccc', marginLeft: '20px' }}>
              RPS: {p.rps} | 4-in-Row: {p.four} | Matching: {p.match}
            </div>
          </li>
        ))}
      </ul>
    );
  };

  return (
    <div>
      <button
        className={`leaderboard ${open ? 'open' : ''}`}
        onClick={handleToggle}
        style={{
          bottom: open ? popupHeight + 20 : 0,
          transition: 'bottom 0.3s ease',
        }}
      >
        Leaderboard
      </button>

      {open && (
        <div ref={popupRef} className="leaderboard-popup">
          {renderPlayers()}
          <button onClick={handleToggle}>Close</button>
        </div>
      )}
    </div>
  );
}
