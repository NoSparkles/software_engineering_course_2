import React from 'react';
import './styles.css';

export default function ScoreBoard({ currentPlayer, scores }) {
  return (
    <div className="scoreboard">
      <h2>Player {currentPlayer}'s Turn</h2>
      <div className="scores">
        <span>Red: {scores.R}</span>
        <span>Yellow: {scores.Y}</span>
      </div>
    </div>
  );
}