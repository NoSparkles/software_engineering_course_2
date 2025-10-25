import React, { useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../Utils/AuthProvider';
import useUserDatabase from '../Utils/useUserDatabase'; // import your hook
import { globalConnectionManager } from '../Utils/GlobalConnectionManager';
import { markLeaveByHome } from '../Utils/ReturnToGameBanner';
import './styles.css';

export default function Navbar() {
  const { user, setUser, logout } = useAuth();
  const { removeExpiredInvites } = useUserDatabase();
  const navigate = useNavigate();
  const location = useLocation();

  // Call removeExpiredInvites on mount and whenever location changes
  useEffect(() => {
    if (user?.username) {
      removeExpiredInvites(user.username)
        .then(updatedUser => {
          setUser(updatedUser)
          console.log("[Navbar] Expired invites removed:", updatedUser);
        })
        .catch(err => console.error("[Navbar] Failed to remove expired invites:", err));
    }
  }, [location.pathname, user?.username]); // triggers on mount and path change

  const handleNavigation = async (path) => {
    const activeGame = localStorage.getItem("activeGame");
    if (activeGame) {
      console.log("[Navbar] Navigating away from active game");
      sessionStorage.setItem("justNavigatedAway", "1");
    }
    navigate(path);
  };

  const handleHomeClick = () => handleNavigation('/');
  const handleProfileClick = () => handleNavigation(`/profile/${user.username}`);
  const handleFriendsClick = () => handleNavigation('/users');

  return (
    <nav className="navbar">
      <div className="navbar-left">
        <button onClick={handleHomeClick} style={{ background: 'none', border: 'none', color: 'white', cursor: 'pointer', textDecoration: 'none', fontSize: 'inherit', fontFamily: 'inherit' }}>
          Home
        </button>
      </div>
      <div className="navbar-right">
        {user?.username ? (
          <>
            <button onClick={handleFriendsClick} style={{ background: 'none', border: 'none', color: 'white', cursor: 'pointer', textDecoration: 'none', fontSize: 'inherit', fontFamily: 'inherit' }}>
              Find Friends
            </button>
            <button onClick={handleProfileClick} style={{ background: 'none', border: 'none', color: 'white', cursor: 'pointer', textDecoration: 'none', fontSize: 'inherit', fontFamily: 'inherit' }}>
              My Profile
            </button>
            <span>Hello, {user.username}</span>
            <button onClick={logout}>Log out</button>
          </>
        ) : (
          <>
            <Link to="/login">Log In</Link>
            <Link to="/register">Sign Up</Link>
          </>
        )}
      </div>
    </nav>
  );
}
