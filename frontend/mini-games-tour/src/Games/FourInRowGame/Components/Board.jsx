import React, { useState, useEffect } from 'react';
import { Slot } from './Slot';
import { createEmptyBoard, checkWin, ROWS, COLS } from '../Logic/connectFourLogic';
import './styles.css'

export const Board = ({ playerColor, connection, roomCode, playerId, spectator = false, token }) => {
    const [board, setBoard] = useState(createEmptyBoard());
    const [currentPlayer, setCurrentPlayer] = useState('R');
    const [winner, setWinner] = useState(null); 

    useEffect(() => {
        if (!connection) return;
        
        const handleReceiveMove = ({ board: newBoard, currentPlayer: nextPlayer, winner: win }) => {
            setBoard(newBoard);
            setCurrentPlayer(nextPlayer);
            setWinner(win);
        };
        
        connection.on("ReceiveMove", handleReceiveMove);
        
        return () => {
            connection.off("ReceiveMove", handleReceiveMove);
        };
    }, [connection, playerId]);

    const handleColumnClick = (col) => {
    if (spectator) return;
    if (winner || currentPlayer !== playerColor) return;
        if (connection) {
            connection.invoke("HandleCommand", "four-in-a-row", roomCode, playerId, `MOVE:${col}`, token);
        }
    };

    const handleReset = () => {
        if (spectator) return;
        if (connection) {
            connection.invoke("HandleCommand", "four-in-a-row", roomCode, playerId, "RESET", token);
        }
    };

    useEffect(() => {
        if (!connection) return;
        
        const handleGameReset = () => {
            setBoard(createEmptyBoard());
            setCurrentPlayer('R');
            setWinner(null);
        };
        
        connection.on("GameReset", handleGameReset);
        
        return () => {
            connection.off("GameReset", handleGameReset);
        };
    }, [connection, playerId]);

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
