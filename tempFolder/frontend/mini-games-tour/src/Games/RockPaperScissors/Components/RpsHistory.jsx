import React from 'react';

export default function RpsHistory({ history }) {
  if (!history?.length) return null;
  return (
    <div style={{ marginTop: 20 }}>
      <h4>Round History</h4>
      <div style={{ display:'grid', gridTemplateColumns:'repeat(4, auto)', gap:8, justifyContent:'center' }}>
        <strong>#</strong><strong>Red</strong><strong>Yellow</strong><strong>Winner</strong>
        {history.map(h => (
          <React.Fragment key={h.round}>
            <span>{h.round}</span>
            <span>{h.R}</span>
            <span>{h.Y}</span>
            <span>{h.winner}</span>
          </React.Fragment>
        ))}
      </div>
    </div>
  );
}
