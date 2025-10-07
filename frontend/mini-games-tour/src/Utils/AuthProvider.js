import React, { createContext, useContext, useState, useEffect } from 'react';
import { setUsernameLocalStorage } from './ReturnToGameBanner';

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(() => localStorage.getItem('token'));

  useEffect(() => {
    if (token) {
      fetch('http://localhost:5236/User/me', {
        headers: { Authorization: `Bearer ${token}` },
      })
        .then(res => res.ok ? res.json() : null)
        .then(data => {
          if (data) {
            setUser(data);
            setUsernameLocalStorage(data.username);
            if (data.playerId) {
              localStorage.setItem('playerId', data.playerId);
            }
            // PATCH: Always save username to localStorage/sessionStorage for session lookup
            if (data.username) {
              localStorage.setItem('username', data.username);
              sessionStorage.setItem('username', data.username);
            }
          }
        });
    }
  }, [token]);

  const login = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });

    if (!response.ok) return false;

    const data = await response.json();
    localStorage.setItem('token', data.token);
    setToken(data.token);
    setUser(data.user);
    setUsernameLocalStorage(username);
    if (data.user && data.user.playerId) {
      localStorage.setItem('playerId', data.user.playerId);
    }
    // PATCH: Always save username to localStorage/sessionStorage for session lookup
    if (data.user && data.user.username) {
      localStorage.setItem('username', data.user.username);
      sessionStorage.setItem('username', data.user.username);
    }
    return true;
  };

  const register = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });

    if (response.ok) {
      setUsernameLocalStorage(username);
      const data = await response.json().catch(() => null);
      if (data && data.playerId) {
        localStorage.setItem('playerId', data.playerId);
      }
      // PATCH: Always save username to localStorage/sessionStorage for session lookup
      if (data && data.username) {
        localStorage.setItem('username', data.username);
        sessionStorage.setItem('username', data.username);
      }
    }
    return response.ok;
  };

  const logout = () => {
    localStorage.removeItem('token');
    setUser(null);
    setToken(null);
    // Don't clear activeGame and roomCloseTime - let the banner handle this
  };

  return (
    <AuthContext.Provider value={{ user, token, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}