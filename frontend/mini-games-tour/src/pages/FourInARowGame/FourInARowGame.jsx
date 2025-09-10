import React from 'react'
import { Link } from 'react-router-dom'
import '../../games/fourInRowGame/components/styles.css'
import {Board} from '../../games/fourInRowGame/components/Board'


const FourInARowGame = () => {
  return (
    <>
      <div className="back-button">
        <Link to="/">Home</Link>
      </div>

      <div>
        <h1>Four In A Row Game</h1>
      </div>

      <div className="four-in-a-row-game">
        <Board />
      </div>

    </>
  )
}

export default FourInARowGame