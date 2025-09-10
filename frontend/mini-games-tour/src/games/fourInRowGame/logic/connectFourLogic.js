export const ROWS = 6;
export const COLS = 7;

export const createEmptyBoard = () => {
    return (
        Array(ROWS).fill(null).map(() => Array(COLS).fill(null))
    )
}

export const checkWin = (board) => {
    // Check horizontal
    for (let r = 0; r < ROWS; r++) {
        for (let c = 0; c <= COLS - 4; c++) {
            const val = board[r][c];
            if (val && val === board[r][c+1] && val === board[r][c+2] && val === board[r][c+3]) {
                return val;
            }
        }
    }
    // Check vertical
    for (let c = 0; c < COLS; c++) {
        for (let r = 0; r <= ROWS - 4; r++) {
            const val = board[r][c];
            if (val && val === board[r+1][c] && val === board[r+2][c] && val === board[r+3][c]) {
                return val;
            }
        }
    }
    // Check diagonal down-right
    for (let r = 0; r <= ROWS - 4; r++) {
        for (let c = 0; c <= COLS - 4; c++) {
            const val = board[r][c];
            if (val && val === board[r+1][c+1] && val === board[r+2][c+2] && val === board[r+3][c+3]) {
                return val;
            }
        }
    }
    // Check diagonal up-right
    for (let r = 3; r < ROWS; r++) {
        for (let c = 0; c <= COLS - 4; c++) {
            const val = board[r][c];
            if (val && val === board[r-1][c+1] && val === board[r-2][c+2] && val === board[r-3][c+3]) {
                return val;
            }
        }
    }
    return null;
};