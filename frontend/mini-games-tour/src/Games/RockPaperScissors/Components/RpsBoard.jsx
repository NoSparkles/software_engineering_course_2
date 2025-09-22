import React, { useMemo, useState, useEffect, useCallback } from 'react';
import RpsScoreBoard from './RpsScoreBoard';
import RpsHistory from './RpsHistory';
import { useRpsEngine } from '../Logic/useRpsEngine';
import '../../FourInRowGame/Components/styles.css';

export default function RpsBoard({ playerColor, connection, roomCode, playerId }) {
  const { state, isMyTurn, choose, reset, resetVote } =
    useRpsEngine({ playerColor, connection, roomCode, playerId });

  const you = playerColor;
  const [selectedChoice, setSelectedChoice] = useState(null);
  const [lastHistLen, setLastHistLen] = useState(0);

  useEffect(() => {
    if (!state) return;

    const histLen = state.history?.length ?? 0;
    if (histLen !== lastHistLen) {
      setSelectedChoice(null);
      setLastHistLen(histLen);
      return;
    }

    if (state.lastDraw && !state.currentChoices?.[you]) {
      setSelectedChoice(null);
    }
  }, [state, you, lastHistLen]);

  const handleChoose = useCallback((c) => {
    if (selectedChoice || !isMyTurn) return;
    setSelectedChoice(c);
    choose(c);
  }, [selectedChoice, isMyTurn, choose]);

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

  const yourServerChoice = state.currentChoices?.[you];
  const isLocked = !!selectedChoice || !!yourServerChoice || !isMyTurn;

  const btn = (c) => {
    const isSelected = selectedChoice === c || yourServerChoice === c;
    return {
      padding: '10px 14px',
      borderRadius: 10,
      border: '1px solid #ccc',
      minWidth: 120,
      fontWeight: 600,
      transition: 'filter 120ms, transform 80ms, opacity 80ms',
      filter: isSelected ? 'brightness(0.85)' : 'none',
      transform: isSelected ? 'scale(0.98)' : 'none',
      opacity: isLocked && !isSelected ? 0.6 : 1,
      cursor: isLocked && !isSelected ? 'not-allowed' : 'pointer',
    };
  };

  const pickedLabel = (c) => c === 'rock' ? '🪨 Rock' : c === 'paper' ? '📄 Paper' : '✂️ Scissors';

  return (
    <div className="game-wrapper">
      <RpsScoreBoard roundLabel={roundLabel} scores={scores} winsToFinish={state.winsToFinish} />

      {!state.winner && (
        <div style={{ marginTop: 20 }}>
          {state.lastDraw && <div style={{ marginBottom: 10, fontWeight: 600 }}>Draw! Pick again.</div>}

          <div style={{ display:'flex', gap:12, justifyContent:'center', marginTop:10 }}>
            <button style={btn('rock')}     disabled={isLocked} onClick={() => handleChoose('rock')}>🪨 Rock</button>
            <button style={btn('paper')}    disabled={isLocked} onClick={() => handleChoose('paper')}>📄 Paper</button>
            <button style={btn('scissors')} disabled={isLocked} onClick={() => handleChoose('scissors')}>✂️ Scissors</button>
          </div>

          {(selectedChoice || yourServerChoice) && (
            <div style={{ marginTop: 10, fontWeight: 600 }}>
              You picked: {pickedLabel(selectedChoice || yourServerChoice)}
            </div>
          )}
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
