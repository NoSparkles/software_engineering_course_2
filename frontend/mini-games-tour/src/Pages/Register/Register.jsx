import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../Utils/AuthProvider';
import { setUsernameLocalStorage, setPlayerIdLocalStorage } from '../../Utils/ReturnToGameBanner';
import './styles.css';

export default function Register() {
  const navigate = useNavigate();
  const { register } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    const success = await register(username, password);
    if (success) {
      setUsernameLocalStorage(username);
     
      if (window.localStorage.getItem("playerId")) {
        setPlayerIdLocalStorage(window.localStorage.getItem("playerId"));
      }
      navigate('/'); 
    } else {
      setError('Username already exists.');
    }
  };

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={handleSubmit}>
        <div className="auth-card__header">
          <p className="eyebrow">Create account</p>
          <h2>Sign Up</h2>
        </div>

        <div className="auth-form-group">
          <label htmlFor="username">Username</label>
          <input
            id="username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </div>

        <div className="auth-form-group">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>

        <div className="auth-form-group">
          <label htmlFor="confirmPassword">Confirm Password</label>
          <input
            id="confirmPassword"
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
          />
        </div>

        {error && <p className="auth-error">{error}</p>}

        <button className="btn btn--primary auth-submit" type="submit">
          Sign Up
        </button>
      </form>
    </div>
  );
}
