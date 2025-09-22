import { useState } from 'react';
import { useAuth } from './AuthProvider'

/* Usage example:

const {
  fetchData,
  setUrl,
  setOptions,
  setAuth,
  data,
  loading,
  error,
} = useFetch();

useEffect(() => {
  setUrl('http://localhost:5236/User/me');
  setOptions({ method: 'GET' });
  setAuth(true);
  fetchData();
}, []);

*/

export default function useFetch(initialUrl = '') {
  const { token } = useAuth();
  const [url, setUrl] = useState(initialUrl);
  const [options, setOptions] = useState({});
  const [auth, setAuth] = useState(false);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchData = async (customUrl = url, customOptions = options, customAuth = auth) => {
    setLoading(true);
    setError(null);

    try {
      const headers = {
        'Content-Type': 'application/json',
        ...(customAuth && token ? { Authorization: `Bearer ${token}` } : {}),
        ...customOptions.headers,
      };

      const response = await fetch(customUrl, {
        ...customOptions,
        headers,
      });

      if (!response.ok) throw new Error(`Error ${response.status}: ${response.statusText}`);

      const result = await response.json();
      setData(result);
      return result;
    } catch (err) {
      setError(err.message || 'Unknown error');
      return null;
    } finally {
      setLoading(false);
    }
  };

  return {
    fetchData,
    setUrl,
    setOptions,
    setAuth,
    data,
    loading,
    error,
  };
}