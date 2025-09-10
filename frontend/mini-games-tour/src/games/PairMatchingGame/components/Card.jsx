import React from 'react';
import './styles.css';

export default function Card({ card, isFlipped, onClick }) {
  return (
    <div className={`card ${isFlipped ? 'flipped' : ''}`} onClick={onClick}>
      {isFlipped ? card.image : '❓'}
    </div>
  );
}