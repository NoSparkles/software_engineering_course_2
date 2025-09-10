import React from 'react'
import { Link } from 'react-router-dom'
import GameBoard from '../../games/PairMatchingGame/components/GameBoard'

const PairMatchingGame = () => {
  return (
    <div>
      <div className="back-button">
        <Link to="/">&#8592; Back to Home</Link>
      </div>
      <h1>Pair Matching Game</h1>
      <GameBoard />
    </div>
  )
}

export default PairMatchingGame