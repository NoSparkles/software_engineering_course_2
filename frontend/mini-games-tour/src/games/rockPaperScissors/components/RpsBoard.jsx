import React, { useMemo } from 'react';
import RpsScoreBoard from './RpsScoreBoard';
import RpsHistory from './RpsHistory';
import { useRpsEngine } from '../logic/useRpsEngine';
import '../../fourInRowGame/components/styles.css';

export default function RpsBoard({ playerColor, connection, roomCode, playerId }) {
  const { state, isMyTurn, choose, reset, resetVote } =
    useRpsEngine({ playerColor, connection, roomCode, playerId });

  const you = playerColor;

  const roundLabel = useMemo(() => {
    if (!state) return 'Round — / —';
    const hasAnyChoice = state.currentChoices.R || state.currentChoices.Y;
    const next = Math.min(state.roundsPlayed + (hasAnyChoice ? 1 : 0), state.maxRounds);
    return `Round ${next} / ${state.maxRounds}`;
  }, [state]);

  const scores = useMemo(() => {
    if (state?.scores && typeof state.scores.R === 'number' && typeof state.scores.Y === 'number') {
      return state.scores;
    }
    const r = state?.history?.filter(h => h.winner === 'R').length || 0;
    const y = state?.history?.filter(h => h.winner === 'Y').length || 0;
    return { R: r, Y: y };
  }, [state]);

  if (!state) return <div className="game-wrapper"><h2>Connecting…</h2></div>;

  return (
    <div className="game-wrapper">
      <RpsScoreBoard
        roundLabel={roundLabel}
        scores={scores}
        winsToFinish={state.winsToFinish}
      />

      {!state.winner && (
        <div style={{ marginTop: 20 }}>
          {state.lastDraw && (
            <div style={{ marginBottom: 10, fontWeight: 600 }}>Draw! Pick again.</div>
          )}
          <p>{isMyTurn ? 'Your move!' : 'Waiting for opponent…'}</p>
          <div style={{ display:'flex', gap:12, justifyContent:'center', marginTop:10 }}>
            <button disabled={!isMyTurn || !!state.currentChoices[you]} onClick={() => choose('rock')}>🪨 Rock</button>
            <button disabled={!isMyTurn || !!state.currentChoices[you]} onClick={() => choose('paper')}>📄 Paper</button>
            <button disabled={!isMyTurn || !!state.currentChoices[you]} onClick={() => choose('scissors')}>✂️ Scissors</button>
          </div>
        </div>
      )}

      {state.winner && (
        <div className="game-over" style={{ display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center', marginTop:20 }}>
          <h3>
            {state.winner === 'DRAW'
              ? '🤝 Match Draw'
              : (state.winner === you ? '🎉 You Win!' : '💀 You Lose')}
          </h3>
          <p>Final Score — Red {scores.R} : Yellow {scores.Y}</p>
          <button className="reset-button" onClick={reset}>Play Again</button>
          {resetVote && <p>Waiting for other player to confirm…</p>}
        </div>
      )}

      <RpsHistory history={state.history} />
    </div>
  );
}
