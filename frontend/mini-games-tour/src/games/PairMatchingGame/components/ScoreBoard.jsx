import React from 'react';
import './styles.css';

export default function ScoreBoard({ currentPlayer, scores }) {
  return (
    <div className="scoreboard">
      <h2>Player {currentPlayer}'s Turn</h2>
      <div className="scores">
        <span>Player 1: {scores[1]}</span>
        <span>Player 2: {scores[2]}</span>
      </div>
    </div>
  );
}