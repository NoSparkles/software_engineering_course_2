// components/GameBoard.jsx
import React from 'react';
import { useGameEngine } from '../logic/useGameEngine';
import Card from './Card';
import ScoreBoard from './ScoreBoard';
import './styles.css';

export default function GameBoard() {
  const {
    cards,
    flipped,
    matched,
    currentPlayer,
    scores,
    gameOver,
    flipCard,
    resetGame
  } = useGameEngine();

  return (
    <div className="game-container">
      <ScoreBoard currentPlayer={currentPlayer} scores={scores} />

      <div className="card-grid">
        {cards.map((card, index) => {
          const isFlipped = flipped.includes(index) || matched.includes(card.id);
          return (
            <Card
              key={card.key}
              card={card}
              isFlipped={isFlipped}
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