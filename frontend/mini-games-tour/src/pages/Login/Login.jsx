import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../Utils/useAuth';
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
      navigate('/'); // ✅ go to homepage
    } else {
      setError('Invalid username or password.');
    }
  };

  return (
    <form className="login-form" onSubmit={handleSubmit}>
      <h2>Log In</h2>

      <div className="login-form-group">
        <label htmlFor="username">Username</label>
        <input
          id="username"
          type="text"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          required
        />
      </div>

      <div className="login-form-group">
        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
      </div>

      <button className="login-btn" type="submit">Log In</button>

      {error && <p className="login-error-message">{error}</p>}
    </form>
  );
}
