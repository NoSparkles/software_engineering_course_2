import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

export function useRpsEngine({ playerColor, connection, roomCode, playerId, connectionState = "Disconnected" }) {
  const [state, setState] = useState(null);
  const [resetVote, setResetVote] = useState(false);
  const colorRef = useRef(playerColor);

  useEffect(() => {
    if (!connection || connectionState !== "Connected") return;

    const onState = (payload) => setState(payload);
    const onReset = (payload) => { setResetVote(false); setState(payload); };

    connection.on('ReceiveRpsState', onState);
    connection.on('RpsReset', onReset);

    connection.invoke('MakeMove', 'rock-paper-scissors', roomCode, playerId, 'getState')
      .catch(() => {});

    return () => {
      connection.off('ReceiveRpsState', onState);
      connection.off('RpsReset', onReset);
    };
  }, [connection, roomCode, playerId, connectionState]);

  const choose = useCallback((what) => {
    if (!connection) return;
    connection.invoke('MakeMove', 'rock-paper-scissors', roomCode, playerId, `CHOOSE:${what}`).catch(() => {});
  }, [connection, roomCode, playerId]);

  const reset = useCallback(() => {
    if (!connection) return;
    setResetVote(true);
    connection.invoke('MakeMove', 'rock-paper-scissors', roomCode, playerId, 'RESET').catch(() => {});
  }, [connection, roomCode, playerId]);

  const isMyTurn = useMemo(() => {
    if (!state) return false;
    const mine = state.currentChoices[colorRef.current];
    return !state.winner && mine == null;
  }, [state]);

  return { state, isMyTurn, choose, reset, resetVote };
}
