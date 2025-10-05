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

  const handleHomeClick = async () => {
    // Check if user is in a game session
    const activeGame = localStorage.getItem("activeGame");
    const hasActiveConnections = globalConnectionManager.hasActiveConnections();

    if (activeGame || hasActiveConnections) {
      try {
        // Mark that Home button is triggering the leave (prevents double delay)
        markLeaveByHome();
        await globalConnectionManager.leaveAllRooms();
      } catch (err) {
        console.warn("LeaveRoom failed:", err);
      }
    }

    // Navigate to home
    navigate('/');
  };

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
            <Link to={'/users'}>Find Friends</Link>
            <Link to={`/profile/${user.username}`}>My Profile</Link>
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
