import { useState, useEffect } from 'react';

export default function useAuth() {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(() => localStorage.getItem('token'));

  useEffect(() => {
    if (token) {
      fetch('http://localhost:5236/User/me', {
        headers: { Authorization: `Bearer ${token}` },
      })
        .then(res => res.ok ? res.json() : null)
        .then(data => data && setUser(data));
    }
  }, [token]);

  const login = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });

    if (!response.ok) return false;

    const jwt = await response.text();
    localStorage.setItem('token', jwt);
    setToken(jwt);

    const meResponse = await fetch('http://localhost:5236/User/me', {
      headers: { Authorization: `Bearer ${jwt}` },
    });

    if (!meResponse.ok) return false;

    const userData = await meResponse.json();
    setUser(userData);
    return true;
  };

  const register = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });

    return response.ok;
  };

  const logout = () => {
    localStorage.removeItem('token');
    setUser(null);
    setToken(null);
  };

  return { user, token, login, register, logout };
}