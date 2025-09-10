import React from 'react'
import { Link } from 'react-router-dom'

const Home = () => {
  return (
    <div className="games">
        <Link to="/rock-paper-scissors">Rock Paper Scissors</Link>
        <Link to="/four-in-a-row">Four In A Row</Link>
        <Link to="/pair-matching">Pair Matching</Link>
    </div>
  )
}

export default Home