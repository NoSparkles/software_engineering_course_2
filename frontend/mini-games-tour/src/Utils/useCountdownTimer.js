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

    // Listen for storage changes (from other tabs/windows)
    const handleStorageChange = (e) => {
      if (e.key === "roomCloseTime") {
        updateCountdown();
      }
    };

    // Listen for custom event (same window)
    const handleLocalStorageUpdate = () => {
      updateCountdown();
    };

    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("localStorageUpdate", handleLocalStorageUpdate);

    return () => {
      clearInterval(interval);
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("localStorageUpdate", handleLocalStorageUpdate);
    };
  }, []);

  return timeLeft;
}


