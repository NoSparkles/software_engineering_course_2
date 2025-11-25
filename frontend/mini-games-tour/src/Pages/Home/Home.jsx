import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import UserNotificationsBanner from '../../Components/UserNotificationBanner';
import useUserDatabase from '../../Utils/useUserDatabase';
import { useAuth } from '../../Utils/AuthProvider';
import './styles.css';

const Home = () => {
  const { getUser } = useUserDatabase();
  const { user } = useAuth();
  const [currentUser, setCurrentUser] = useState(null);
  const [loading, setLoading] = useState(false);

  const rps = Number(currentUser?.rockPaperScissorsMMR ?? currentUser?.RockPaperScissorsMMR ?? 0);
  const four = Number(currentUser?.fourInARowMMR ?? currentUser?.FourInARowMMR ?? 0);
  const match = Number(currentUser?.pairMatchingMMR ?? currentUser?.PairMatchingMMR ?? 0);
  const totalPoints = rps + four + match;
  useEffect(() => {
    if (!user?.username) {
      setCurrentUser(null);
      return;
    }

    let cancelled = false;
    const fetchProfile = async () => {
      setLoading(true);
      try {
        const result = await getUser(user.username);
        if (!cancelled) setCurrentUser(result ?? null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchProfile();
    return () => { cancelled = true; };
  }, [user?.username]); 

  return (
    <>
      <UserNotificationsBanner />
      <div className='home-content'>
        <section className="games-panel">
          <Link className="game-card" to="/rock-paper-scissors">Rock Paper Scissors</ Link>
          <Link className="game-card" to="/pair-matching">Pair Matching</Link>
          <Link className="game-card" to="/four-in-a-row">Four In A Row</Link>
        </section>

        <section className="stats-panel">
          {loading && <p>Loading profile…</p>}
          {!loading && !currentUser && <p>Sign in to view your stats.</p>}
          {!loading && currentUser && (
          <div className="stats-panel__content">
            <div className="stats-panel__total">Total matchmaking points: {totalPoints}</div>
            <div className='game-info'>
              <h2>Rock Paper Scissors</h2>
              <span className='game-mmr'>MMR: {rps}</span>
              <span className='game-streak'>Win Streak: {currentUser. rockPaperScissorsWinStreak}</span>
            </div>
            <div className='game-info'>
              <h2>Pair Matching</h2>
              <span className='game-mmr'>MMR: {match}</span>
              <span className='game-streak'>Win Streak: {currentUser.   pairMatchingWinStreak}</span>
            </div>
            <div className='game-info'>
              <h2>Four In a Row</h2>
              <span className='game-mmr'>MMR: {four}</span>
              <span className='game-streak'>Win Streak: {currentUser. fourInARowWinStreak}</span>
          </div>
          </div>
          )}
        </section>
      </div>
    </>
  );
};

export default Home;