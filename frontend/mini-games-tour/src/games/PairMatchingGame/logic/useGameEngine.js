  import { useState, useEffect } from 'react';
  export function useGameEngine({ playerColor, connection, roomCode, playerId }) {
    const [cards, setCards] = useState([]);
    const [flipped, setFlipped] = useState([]);
    const [currentPlayer, setCurrentPlayer] = useState('Red');
    const [scores, setScores] = useState({ R: 0, Y: 0 });
    const [winner, setWinner] = useState(null)

    let changePlayer = false
    
    //let changePlayer = false

    const gameType = 'pair-matching'

    useEffect(() => {
      if (!connection) {
        console.log("not connected")
        return
      }
        console.log("receiving board")
        connection.on("receiveBoard", receiveBoard)

        console.log("getting board")
        connection.invoke("makeMove", gameType, roomCode, playerId, 'getBoard')
    }, [connection]);

    useEffect(() => {
      if (!connection) return
    }, [connection])

    useEffect(() => {
      console.log("Flipped: ", flipped)
      if (flipped.length === 2) {
        const [first, second] = flipped;
        if (cards[first].value === cards[second].value) {
          setCards(prev =>
            prev.map((card, i) => {
              if (i === first || i === second) {
                return { ...card, state: "Matched" };
              }
              return card;
            })
          );

          setFlipped([]);
        } else {
          // Flip back after delay
          setTimeout(() => {
            setCards(prev =>
              prev.map((card, i) => {
                if (i === first || i === second) {
                  return { ...card, state: "FaceDown" };
                }
                return card;
              })
            );

            setFlipped([]);
            if (changePlayer) {
              setCurrentPlayer(currentPlayer === "Red" ? "Yellow" : "Red");
            }
            
          }, 1000);
        }
      }
    }, [flipped]);

    const receiveBoard = gameState => {
      console.log(gameState)
      setCards(gameState.board)
      setFlipped(gameState.flipped)
      setCurrentPlayer(gameState.currentPlayer === "R" ? "Red" : "Yellow")
      setScores(gameState.scores)
      setWinner(gameState.winner)

      if (gameState.flipped.length === 2) {
        changePlayer = false
      }
    }

    const flipCard = index => {
      if (winner) return
      console.log(currentPlayer, playerColor)
      let col = Math.floor(index % 6)
      let row = Math.floor(index / 6)
      console.log(row, col, index)
      if (
        currentPlayer === (playerColor === "R" ? "Red" : "Yellow") &&
        flipped.length < 2 &&
        cards[index].state === "FaceDown"
      ) {
        setFlipped(prev => [...prev, index]);;
        setCards(prev => {
          return prev.map((card, i) => {
            if (i === index) {
              return {
                ...card,
                state: "FaceUp"
              };
            }
            return card;
          });
        });
        if (!connection) return
        connection.invoke("makeMove", gameType, roomCode, playerId, `flip ${col} ${row}`)
      }
    };

    const resetGame = () => {
      setCards(); // todo
      setFlipped([]);
      setScores({ R: 0, Y: 0 });
      setCurrentPlayer(1);
      setWinner(null);
    };


    return {
      cards,
      flipped,
      currentPlayer,
      scores,
      winner,
      flipCard,
      resetGame
    };
  }