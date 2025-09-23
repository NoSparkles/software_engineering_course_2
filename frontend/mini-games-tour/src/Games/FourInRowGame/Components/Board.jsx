import React, { useState, useEffect } from 'react';
import { Slot } from './Slot';
import { createEmptyBoard, checkWin, ROWS, COLS } from '../Logic/connectFourLogic';
import './styles.css'

export const Board = ({ playerColor, connection, roomCode, playerId, spectator = false }) => {
    const [board, setBoard] = useState(createEmptyBoard());
    const [currentPlayer, setCurrentPlayer] = useState('R');
    const [winner, setWinner] = useState(null); 

    useEffect(() => {
        if (!connection) return;
        connection.on("ReceiveMove", ({ board: newBoard, currentPlayer: nextPlayer, winner: win }) => {
            setBoard(newBoard);
            setCurrentPlayer(nextPlayer);
            setWinner(win);
        });
        return () => {
            connection.off("ReceiveMove");
        };
    }, [connection]);

    const handleColumnClick = (col) => {
    if (spectator) return;
    if (winner || currentPlayer !== playerColor) return;
        if (connection) {
            connection.invoke("MakeMove", "four-in-a-row", roomCode, playerId, `MOVE:${col}`);
        }
    };

    const handleReset = () => {
        if (spectator) return;
        if (connection) {
            connection.invoke("MakeMove", "four-in-a-row", roomCode, playerId, "RESET");
        }
    };

    useEffect(() => {
    if (!connection) return;
    connection.on("GameReset", () => {
        setBoard(createEmptyBoard());
        setCurrentPlayer('R');
        setWinner(null);
    });
    return () => {
        connection.off("GameReset");
    };
}, [connection]);

    return (
        <div className='four-in-a-row-game'>
            <div style={{ marginBottom: '10px' }}>
                {winner ? (
                    <span>
                        <div className='winnerText' style={{
                            backgroundColor: winner === 'R' ? 'red' : 'yellow',
                            color: '#fff',
                        }}>
                            Winner: {winner === 'R' ? 'Red' : 'Yellow'}    
                        </div>
                    </span>
                ) : (
                    <span>Current Player: {currentPlayer === 'R' ? 'Red' : 'Yellow'}</span>
                )}
                <button style={{ marginLeft: '20px' }} onClick={handleReset} disabled={spectator}>Reset</button>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: `repeat(${COLS}, 50px)` }}>
                {board.map((row, rowIdx) =>
                    row.map((cell, colIdx) => (
                        <Slot
                            key={rowIdx + '-' + colIdx}
                            value={cell}
                            onClick={() => handleColumnClick(colIdx)}
                            isClickable={!winner && board[0][colIdx] === null && currentPlayer === playerColor}
                        />
                    ))
                )}
            </div>
        </div>
    );
};
