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
    // Check if user is in a game session
    const activeGame = localStorage.getItem("activeGame");
    const hasActiveConnections = globalConnectionManager.hasActiveConnections();

    if (activeGame || hasActiveConnections) {
      try {
        // Mark that navigation is triggering the leave
        markLeaveByHome();
        await globalConnectionManager.leaveAllRooms();
      } catch (err) {
        console.warn("LeaveRoom failed:", err);
      }
    }

    // Navigate to the specified path
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
