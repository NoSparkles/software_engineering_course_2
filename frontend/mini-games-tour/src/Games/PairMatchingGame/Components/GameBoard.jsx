import React from 'react';
import { useGameEngine } from '../Logic/useGameEngine';
import Card from './Card';
import ScoreBoard from './ScoreBoard';
import './styles.css';

export default function GameBoard({ playerColor, connection, roomCode, playerId, connectionState, spectator }) {
  const {
    cards,
    flipped,
    currentPlayer,
    scores,
    winner,
    resetVote,
    flipCard,
    resetGame
  } = useGameEngine({playerColor, connection, roomCode, playerId, connectionState});

  return (
    <div className="game-container">
      <ScoreBoard currentPlayer={currentPlayer} scores={scores} />

      <div className="card-grid">
        {cards.map((card, index) => {
          return (
            <Card
              key={index}
              card={card}
              onClick={() => {
                if (spectator) return; // spectators cannot flip
                flipCard(index);
              }}
              disabled={!!spectator}
            />
          );
        })}
      </div>

      { winner && (
        <div className="game-over" style={{backgroundColor:'#0d111d'}}>
          <h3>🎉 Player {scores[1] === 5 ? 1 : 2} Wins!</h3>
          <button onClick={resetGame} className="reset-button">Play Again</button>
          {resetVote && (
            <p>Waiting for other player to confirm</p>
          )}
        </div>
      )}
    </div>
  );
}