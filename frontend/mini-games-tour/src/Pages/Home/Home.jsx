import React, { useEffect } from 'react'
import { Link } from 'react-router-dom'
import './styles.css'

const Home = () => {
  useEffect(() => {
    // Trigger banner check when arriving at home page
    setTimeout(() => {
      window.dispatchEvent(new Event("localStorageUpdate"));
    }, 100);
  }, []);

  return (
    <div className="games">
      <Link className="game-card" to="/rock-paper-scissors">Rock Paper Scissors</Link>

      <Link className="game-card" to="/pair-matching">Pair Matching</Link>

      <Link className="game-card" to="/four-in-a-row">Four In A Row</Link>

      {/*<Link className="game-card disabled" >Tournament (Coming Soon)</Link>*/}
    </div>
  )
}

export default Home
