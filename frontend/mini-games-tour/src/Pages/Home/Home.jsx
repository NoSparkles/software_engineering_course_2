import React from 'react'
import { Link } from 'react-router-dom'
import './styles.css'
import { ReturnToGameBanner } from '../../Utils/ReturnToGameBanner'
const Home = () => {
  return (
    <div className="games">
      <ReturnToGameBanner />
      <Link className="game-card" to="/rock-paper-scissors">Rock Paper Scissors</Link>

      <Link className="game-card" to="/pair-matching">Pair Matching</Link>

      <Link className="game-card" to="/four-in-a-row">Four In A Row</Link>

      {/*<Link className="game-card disabled" >Tournament (Coming Soon)</Link>*/}
    </div>
  )
}

export default Home
