import React, { createContext, useContext, useState, useEffect } from 'react';
import { setUsernameLocalStorage } from './ReturnToGameBanner';
import { useLocation } from 'react-router-dom';

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(() => localStorage.getItem('token'));
  const location = useLocation(); // track current route

  // Fetch user data
  const fetchUser = async () => {
    if (!token) return;
    try {
      const res = await fetch('http://localhost:5236/User/me', {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!res.ok) return;
      const data = await res.json();
      if (data) {
        setUser({ ...data }); // make sure to create a new object for re-render
        setUsernameLocalStorage(data.username);
        if (data.playerId) localStorage.setItem('playerId', data.playerId);
        if (data.username) {
          localStorage.setItem('username', data.username);
          sessionStorage.setItem('username', data.username);
        }
      }
    } catch (err) {
      console.error('Failed to fetch user:', err);
    }
  };

  // Fetch on initial load
  useEffect(() => {
    fetchUser();
  }, [token]);

  // Fetch whenever route changes
  useEffect(() => {
    fetchUser();
  }, [location.pathname]);

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
    if (data.user && data.user.playerId) localStorage.setItem('playerId', data.user.playerId);
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
      if (data && data.playerId) localStorage.setItem('playerId', data.playerId);
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
    localStorage.removeItem("declinedReconnectionFlag")
    localStorage.removeItem("playerId")
    localStorage.removeItem("username")
    sessionStorage.removeItem("playerId")
    sessionStorage.removeItem("username")
    setToken(null);
  };

  return (
    <AuthContext.Provider value={{ user, setUser, token, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
