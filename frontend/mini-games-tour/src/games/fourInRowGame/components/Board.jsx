import React, { useState } from 'react';
import { Slot } from '../components/Slot';
import { createEmptyBoard, checkWin, ROWS, COLS } from '../logic/connectFourLogic';

export const Board = () => {
    const [board, setBoard] = useState(createEmptyBoard());
    const [currentPlayer, setCurrentPlayer] = useState('R');
    const [winner, setWinner] = useState(null);

    const handleColumnClick = (col) => {
        if (winner) return;
        for (let row = ROWS - 1; row >= 0; row--) {
            if (!board[row][col]) {
                const newBoard = board.map(rowArr => [...rowArr]);
                newBoard[row][col] = currentPlayer;
                setBoard(newBoard);
                const win = checkWin(newBoard);
                if (win) {
                    setWinner(win);
                } else {
                    setCurrentPlayer(currentPlayer === 'R' ? 'Y' : 'R');
                }
                break;
            }
        }
    };

    const handleReset = () => {
        setBoard(createEmptyBoard());
        setCurrentPlayer('R');
        setWinner(null);
    };

    return (
        <div>
            <div style={{ marginBottom: '10px' }}>
                {winner ? (
                    <span>Winner: {winner === 'R' ? 'Red' : 'Yellow'}</span>
                ) : (
                    <span>Current Player: {currentPlayer === 'R' ? 'Red' : 'Yellow'}</span>
                )}
                <button style={{ marginLeft: '20px' }} onClick={handleReset}>Reset</button>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: `repeat(${COLS}, 50px)` }}>
                {board.map((row, rowIdx) =>
                    row.map((cell, colIdx) => (
                        <Slot
                            key={rowIdx + '-' + colIdx}
                            value={cell}
                            onClick={() => handleColumnClick(colIdx)}
                            isClickable={!winner && board[0][colIdx] === null}
                        />
                    ))
                )}
            </div>
        </div>
    );
};
