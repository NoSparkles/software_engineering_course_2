import { useState } from 'react';

export function useAuth() {
  const [user, setUser] = useState(null);

  const login = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/' + username);
    if (!response.ok) return false;

    const data = await response.json();
    // Check password (in real apps, you'd use JWT)
    if (data.passwordHash !== password) return false;

    setUser({ username: data.username });
    return true;
  };

  const register = async (username, password) => {
    const response = await fetch('http://localhost:5236/User/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password: password }),
    });

    return response.ok;
  };

  const logout = () => {
    setUser(null);
  };

  return { user, login, register, logout };
}
