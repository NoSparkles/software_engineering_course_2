import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../Utils/AuthProvider';
import { setUsernameLocalStorage, setPlayerIdLocalStorage } from '../../Utils/ReturnToGameBanner';
import './styles.css';

export default function Login() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();

    const success = await login(username, password);
    if (success) {
      setUsernameLocalStorage(username); 
      if (window.localStorage.getItem("playerId")) {
        setPlayerIdLocalStorage(window.localStorage.getItem("playerId"));
      }
      navigate('/'); 
    } else {
      setError('Invalid username or password.');
    }
  };

  return (
    <div className="auth-page page-shell">
      <form className="auth-card card" onSubmit={handleSubmit}>
        <div className="auth-card__header">
          <p className="eyebrow">Welcome back</p>
          <h2>Log In</h2>
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

        {error && <p className="auth-error">{error}</p>}

        <button className="btn btn--primary auth-submit" type="submit">
          Log In
        </button>
      </form>
    </div>
  );
}
