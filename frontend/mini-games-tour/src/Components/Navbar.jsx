import React from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../Utils/AuthProvider';
import { globalConnectionManager } from '../Utils/GlobalConnectionManager';
import { markLeaveByHome } from '../Utils/ReturnToGameBanner';
import './styles.css';

export default function Navbar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleNavigation = async (path) => {
    // Mark that we're navigating away from a game
    // This helps the Return to Game Banner show immediately
    const activeGame = localStorage.getItem("activeGame");
    if (activeGame) {
      console.log("[Navbar] Navigating away from active game");
      // Set a navigation flag so banner knows to check after navigation completes
      sessionStorage.setItem("justNavigatedAway", "1");
    }
    
    // Just navigate - SignalR will naturally disconnect via OnDisconnectedAsync
    // which will mark player as disconnected and allow reconnection
    // The Return to Game Banner will appear with options to reconnect or decline
    navigate(path);
  };

  const handleHomeClick = () => handleNavigation('/');
  const handleProfileClick = () => handleNavigation(`/profile/${user.username}`);
  const handleFriendsClick = () => handleNavigation('/users');

  return (
    <nav className="navbar">
      <div className="navbar-left">
        <button 
          onClick={handleHomeClick}
          style={{
            background: 'none',
            border: 'none',
            color: 'white',
            cursor: 'pointer',
            textDecoration: 'none',
            fontSize: 'inherit',
            fontFamily: 'inherit'
          }}
        >
          Home
        </button>
      </div>
      <div className="navbar-right">
        {user?.username ? (
          <>
            <button 
              onClick={handleFriendsClick}
              style={{
                background: 'none',
                border: 'none',
                color: 'white',
                cursor: 'pointer',
                textDecoration: 'none',
                fontSize: 'inherit',
                fontFamily: 'inherit'
              }}
            >
              Find Friends
            </button>
            <button 
              onClick={handleProfileClick}
              style={{
                background: 'none',
                border: 'none',
                color: 'white',
                cursor: 'pointer',
                textDecoration: 'none',
                fontSize: 'inherit',
                fontFamily: 'inherit'
              }}
            >
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
