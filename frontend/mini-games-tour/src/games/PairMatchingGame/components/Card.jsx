import React from 'react';
import './styles.css';

export default function Card({ card, onClick }) {
  return (
    <div className={`card ${card.state}`} onClick={onClick}>
      {card.state === "FaceDown" ? card.value : '❓'}
    </div>
  );
}