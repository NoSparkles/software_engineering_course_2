import React from 'react';
import { useGameEngine } from '../Logic/useGameEngine';
import Card from './Card';
import ScoreBoard from './ScoreBoard';
import './styles.css';

export default function GameBoard({ playerColor, connection, roomCode, playerId, connectionState, spectator = false }) {
  const {
    cards,
    flipped,
    currentPlayer,
    scores,
    winner,
    resetVote,
    flipCard,
    resetGame
  } = useGameEngine({playerColor, connection, roomCode, playerId, connectionState, spectator});

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
                if (spectator) return;
                flipCard(index);
              }}
            />
          );
        })}
      </div>

      { winner && (
        <div className="game-over">
          <h3>{spectator ? `Winner: ${scores.R > scores.Y ? 'Red' : scores.Y > scores.R ? 'Yellow' : 'Draw'}` : `🎉 Player ${scores[1] === 5 ? 1 : 2} Wins!`}</h3>
          {!spectator && <button onClick={resetGame}>Play Again</button>}
          {resetVote && (
            <p>Waiting for other player to confirm</p>
          )}
        </div>
      )}
    </div>
  );
}