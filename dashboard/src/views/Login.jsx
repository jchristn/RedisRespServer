import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import './Login.css';

export default function Login() {
  const navigate = useNavigate();
  const { connect, isConnecting, error, theme, toggleTheme, redisConnection: savedConnection } = useApp();
  const [connection, setConnection] = useState(savedConnection || 'localhost:6379');

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!connection.trim()) return;

    const success = await connect(connection.trim());
    if (success) {
      navigate('/overview');
    }
  };

  return (
    <div className="login-container">
      <div className="login-header">
        <button
          className="btn-icon theme-toggle"
          onClick={toggleTheme}
          title={theme === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>
      </div>

      <div className="login-card">
        <div className="login-logo">
          <span className="logo-icon">R</span>
          <h1 className="logo-text">Redish Dashboard</h1>
        </div>

        <p className="login-description">
          Connect to your Redis or Redish server to manage keys, monitor performance, and execute commands.
        </p>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="connection" className="form-label">Redis Server</label>
            <input
              type="text"
              id="connection"
              className="input"
              placeholder="localhost:6379"
              value={connection}
              onChange={(e) => setConnection(e.target.value)}
              required
              autoFocus
            />
            <p className="form-hint">
              Enter host:port (e.g., localhost:6379) or redis://host:port
            </p>
          </div>

          {error && (
            <div className="login-error">
              {error}
            </div>
          )}

          <button
            type="submit"
            className="btn btn-primary login-btn"
            disabled={isConnecting || !connection.trim()}
          >
            {isConnecting ? (
              <>
                <span className="loading-spinner"></span>
                Connecting...
              </>
            ) : (
              'Connect'
            )}
          </button>
        </form>

        <div className="login-footer">
          <p className="login-footer-text">
            Need help? Check the{' '}
            <a href="https://redis.io/docs/" target="_blank" rel="noopener noreferrer">
              Redis documentation
            </a>
          </p>
        </div>
      </div>

      <div className="login-features">
        <div className="feature">
          <span className="feature-icon">🔑</span>
          <span className="feature-text">Browse & manage keys</span>
        </div>
        <div className="feature">
          <span className="feature-icon">📊</span>
          <span className="feature-text">Monitor performance</span>
        </div>
        <div className="feature">
          <span className="feature-icon">💻</span>
          <span className="feature-text">Execute commands</span>
        </div>
        <div className="feature">
          <span className="feature-icon">👥</span>
          <span className="feature-text">View connections</span>
        </div>
      </div>
    </div>
  );
}
