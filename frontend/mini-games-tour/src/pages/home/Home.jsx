import React from 'react'
import { Link } from 'react-router-dom'
import './styles.css'

const Home = () => {
  return (
    <div className="games">
      <div className="game-card">
        <Link to="/rock-paper-scissors">Rock Paper Scissors</Link>
      </div>

      <div className="game-card">
        <Link to="/pair-matching">Pair Matching</Link>
      </div>

      <div className="game-card">
        <Link to="/four-in-a-row">Four In A Row</Link>
      </div>

      <div className="game-card disabled">
        <span>Tournament (Coming Soon)</span>
      </div>
    </div>
  )
}

export default Home
