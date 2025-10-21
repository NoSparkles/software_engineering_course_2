import React from 'react';
import './styles.css';

export default function Card({ card, onClick, disabled }) {
  return (
    <div
      className={`card ${card.state} ${disabled ? 'disabled' : ''}`}
      onClick={disabled ? undefined : onClick}
      aria-disabled={disabled}
      role="button"
    >
      {card.state !== "FaceDown" ? card.value : '❓'}
    </div>
  );
}