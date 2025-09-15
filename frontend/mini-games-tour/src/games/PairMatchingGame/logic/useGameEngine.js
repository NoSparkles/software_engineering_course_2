import { useState, useEffect } from 'react';
export function useGameEngine({ playerColor, connection, roomCode, playerId }) {
  const [cards, setCards] = useState([]);
  const [flipped, setFlipped] = useState([]);
  const [matched, setMatched] = useState([]);
  const [currentPlayer, setCurrentPlayer] = useState(1);
  const [scores, setScores] = useState({ 1: 0, 2: 0 });
  const [gameOver, setGameOver] = useState(false);

  const gameType = 'pair-matching'

  useEffect(() => {
    if (!connection) return
      connection.on("receiveBoard", receiveBoard)

      connection.invoke("makeMove", gameType, roomCode, playerId, 'getBoard')
  }, [connection]);

  useEffect(() => {
    if (!connection) return
  }, [connection])

  useEffect(() => {
    if (flipped.length === 2) {
      const [first, second] = flipped;
      if (cards[first].id === cards[second].id) {
        setMatched([...matched, cards[first].id]);
        setScores(prev => ({
          ...prev,
          [currentPlayer]: prev[currentPlayer] + 1
        }));
        if (scores[currentPlayer] + 1 === 5) {
          setGameOver(true);
        }
        setFlipped([]);
      } else {
        setTimeout(() => {
          setFlipped([]);
          setCurrentPlayer(currentPlayer === 1 ? 2 : 1);
        }, 1000);
      }
    }
  }, [flipped]);

  const receiveBoard = cards => {
    setCards(cards)
    console.log("cards ", cards)
  }

  const flipCard = index => {
    if (
      flipped.length < 2 &&
      !flipped.includes(index) &&
      !matched.includes(cards[index].id)
    ) {
      setFlipped([...flipped, index]);
    }
  };

  const resetGame = () => {
    setCards(); // todo
    setFlipped([]);
    setMatched([]);
    setScores({ 1: 0, 2: 0 });
    setCurrentPlayer(1);
    setGameOver(false);
  };


  return {
    cards,
    flipped,
    matched,
    currentPlayer,
    scores,
    gameOver,
    flipCard,
    resetGame
  };
}