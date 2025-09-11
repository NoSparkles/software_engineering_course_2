import { useEffect, useState } from 'react';

export function usePlayerId() {
  const [playerId, setPlayerId] = useState(null);

  useEffect(() => {
    let storedId = localStorage.getItem("playerId");

    if (!storedId) {
      storedId = crypto.randomUUID(); // Generates a unique ID
      localStorage.setItem("playerId", storedId);
    }

    setPlayerId(storedId);
  }, []);

  return playerId;
}