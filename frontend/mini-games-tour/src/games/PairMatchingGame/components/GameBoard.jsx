import React from 'react';
import { useGameEngine } from '../logic/useGameEngine';
import Card from './Card';
import ScoreBoard from './ScoreBoard';
import './styles.css';

export default function GameBoard({ playerColor, connection, roomCode, playerId }) {
  const {
    cards,
    flipped,
    matched,
    currentPlayer,
    scores,
    gameOver,
    flipCard,
    resetGame
  } = useGameEngine({playerColor, connection, roomCode, playerId});

  return (
    <div className="game-container">
      <ScoreBoard currentPlayer={currentPlayer} scores={scores} />

      <div className="card-grid">
        {cards.map((card, index) => {
          return (
            <Card
              key={index}
              card={card}
              onClick={() => flipCard(index)}
            />
          );
        })}
      </div>

      { gameOver && (
        <div className="game-over">
          <h3>🎉 Player {scores[1] === 5 ? 1 : 2} Wins!</h3>
          <button onClick={resetGame}>Play Again</button>
        </div>
      )}
    </div>
  );
}