import React from 'react'
import '../../games/fourInRowGame/components/styles.css'
import {Board} from '../../games/fourInRowGame/components/Board'


const FourInARowGame = () => {
  return (
    <>
      <div className="back-button">
        <a href="/">&#8592; Back to Home</a>
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