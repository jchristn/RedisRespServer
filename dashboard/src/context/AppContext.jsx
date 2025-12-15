import { createContext, useContext, useState, useEffect, useMemo } from 'react';
import { RedisApi } from '../utils/api';

const AppContext = createContext(null);

const STORAGE_KEYS = {
  REDIS_CONNECTION: 'redish_connection',
  THEME: 'redish_theme',
  SELECTED_DB: 'redish_selected_db',
};

export function AppProvider({ children }) {
  const [redisConnection, setRedisConnection] = useState(() =>
    localStorage.getItem(STORAGE_KEYS.REDIS_CONNECTION) || ''
  );
  const [theme, setTheme] = useState(() =>
    localStorage.getItem(STORAGE_KEYS.THEME) || 'light'
  );
  const [selectedDb, setSelectedDb] = useState(() =>
    parseInt(localStorage.getItem(STORAGE_KEYS.SELECTED_DB) || '0', 10)
  );
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [error, setError] = useState(null);
  const [serverInfo, setServerInfo] = useState(null);

  // Create API instance when connection changes
  const api = useMemo(() => {
    if (!redisConnection) return null;
    return new RedisApi(redisConnection);
  }, [redisConnection]);

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(STORAGE_KEYS.THEME, theme);
  }, [theme]);

  // Save connection to localStorage
  useEffect(() => {
    if (redisConnection) {
      localStorage.setItem(STORAGE_KEYS.REDIS_CONNECTION, redisConnection);
    }
  }, [redisConnection]);

  // Save selected DB to localStorage
  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.SELECTED_DB, selectedDb.toString());
  }, [selectedDb]);

  // Try to reconnect on load if we have a connection
  useEffect(() => {
    if (redisConnection && !isConnected && !isConnecting) {
      validateConnection();
    }
  }, []);

  const toggleTheme = () => {
    setTheme(prev => prev === 'light' ? 'dark' : 'light');
  };

  const validateConnection = async () => {
    if (!api) return false;

    setIsConnecting(true);
    setError(null);

    try {
      const response = await api.ping();
      if (response === 'PONG' || response === true) {
        setIsConnected(true);
        // Fetch server info after successful connection
        try {
          const info = await api.getInfo();
          setServerInfo(info);
        } catch {
          // Server info is optional
        }
        return true;
      }
      throw new Error('Invalid server response');
    } catch (err) {
      setError(err.message);
      setIsConnected(false);
      return false;
    } finally {
      setIsConnecting(false);
    }
  };

  const connect = async (connection) => {
    setRedisConnection(connection);
    const newApi = new RedisApi(connection);

    setIsConnecting(true);
    setError(null);

    try {
      const response = await newApi.ping();
      if (response === 'PONG' || response === true) {
        setIsConnected(true);
        // Fetch server info after successful connection
        try {
          const info = await newApi.getInfo();
          setServerInfo(info);
        } catch {
          // Server info is optional
        }
        return true;
      }
      throw new Error('Invalid server response');
    } catch (err) {
      setError(err.message);
      setIsConnected(false);
      return false;
    } finally {
      setIsConnecting(false);
    }
  };

  const disconnect = () => {
    setIsConnected(false);
    setRedisConnection('');
    setServerInfo(null);
    setSelectedDb(0);
    localStorage.removeItem(STORAGE_KEYS.REDIS_CONNECTION);
  };

  const selectDatabase = async (db) => {
    if (!api) return;
    try {
      await api.selectDatabase(db);
      setSelectedDb(db);
    } catch (err) {
      setError(err.message);
    }
  };

  const refreshServerInfo = async () => {
    if (!api) return;
    try {
      const info = await api.getInfo();
      setServerInfo(info);
    } catch (err) {
      console.error('Failed to refresh server info:', err);
    }
  };

  const value = {
    // State
    serverUrl: redisConnection, // Keep for backward compatibility
    redisConnection,
    theme,
    isConnected,
    isConnecting,
    error,
    api,
    serverInfo,
    selectedDb,

    // Actions
    toggleTheme,
    connect,
    disconnect,
    setError,
    selectDatabase,
    refreshServerInfo,
  };

  return (
    <AppContext.Provider value={value}>
      {children}
    </AppContext.Provider>
  );
}

export function useApp() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return context;
}

export default AppContext;
