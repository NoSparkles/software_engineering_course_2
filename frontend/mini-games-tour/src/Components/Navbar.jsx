import React, { useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../Utils/AuthProvider';
import useUserDatabase from '../Utils/useUserDatabase';
import './styles.css';

export default function Navbar() {
  const { user, setUser, logout } = useAuth();
  const { removeExpiredInvites } = useUserDatabase();
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (user?.username) {
      removeExpiredInvites(user.username)
        .then(updatedUser => setUser(updatedUser))
        .catch(err => console.error('[Navbar] Failed to remove expired invites:', err));
    }
  }, [location.pathname, user?.username]);

  const handleNavigation = (path) => {
    const activeGame = localStorage.getItem('activeGame');
    if (activeGame) {
      sessionStorage.setItem('justNavigatedAway', '1');
    }
    navigate(path);
  };

  const handleHomeClick = () => handleNavigation('/');
  const handleProfileClick = () => user?.username && handleNavigation(`/profile/${user.username}`);
  const handleFriendsClick = () => handleNavigation('/users');

  return (
    <nav className="navbar">
      <div className="navbar__brand" onClick={handleHomeClick}>
        <div className="navbar__logo">MiniGames</div>
      </div>


      <div className="navbar__actions">
        {user?.username ? (
          <>
            <span className="navbar__greeting">Hello, {user.username}</span>
            <button type="button" className="btn btn--ghost" onClick={handleProfileClick}>My Profile</button>
            <button type="button" className="btn btn--ghost" onClick={handleFriendsClick}>Find Friends</button>
            <button type="button" className="btn btn--primary" onClick={logout}>Log out</button>
          </>
        ) : (
          <>
            <Link className="btn btn--ghost" to="/login">Log In</Link>
            <Link className="btn btn--primary" to="/register">Sign Up</Link>
          </>
        )}
      </div>
    </nav>
  );
}
