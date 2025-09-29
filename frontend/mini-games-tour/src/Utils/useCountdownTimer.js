import { useEffect, useState } from 'react';

export function useCountdownTimer() {
  const [timeLeft, setTimeLeft] = useState(null);

  useEffect(() => {
    const updateCountdown = () => {
      const roomCloseTime = localStorage.getItem("roomCloseTime");
      
      if (!roomCloseTime) {
        setTimeLeft(null);
        return;
      }

      const now = new Date().getTime();
      const closeTime = new Date(roomCloseTime).getTime();
      const difference = closeTime - now;

      if (difference > 0) {
        const seconds = Math.floor(difference / 1000);
        setTimeLeft(seconds);
      } else {
        setTimeLeft(0);
        localStorage.removeItem("roomCloseTime");
      }
    };

    // Update immediately
    updateCountdown();

    // Update every second
    const interval = setInterval(updateCountdown, 1000);

    return () => clearInterval(interval);
  }, []);

  return timeLeft;
}


