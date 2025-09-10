import React from 'react'
import { Link } from 'react-router-dom'

const Home = () => {
  return (
    <div className="games">
      <div>
        <Link to="/rock-paper-scissors">Rock Paper Scissors</Link>
      </div>

      <div>
        <Link to="/pair-matching">Pair Matching</Link>
      </div>

      <div>
        <Link to="/four-in-a-row">Four In A Row</Link>
      </div>
    </div>
    
  )
}

export default Home