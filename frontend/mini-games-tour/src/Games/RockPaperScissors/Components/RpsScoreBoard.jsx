import React from 'react';

export default function RpsScoreBoard({
  roundLabel,
  scores,
  winsToFinish
}) {
  return (
    <div className="scoreboard">
      <h2>{roundLabel} — First to {winsToFinish}</h2>
      <div className="scores" style={{ marginTop: 8 }}>
        <strong>Red:</strong> {scores?.R ?? 0} &nbsp;&nbsp;
        <strong>Yellow:</strong> {scores?.Y ?? 0}
      </div>
    </div>
  );
}
