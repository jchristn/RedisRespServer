import { useApp } from '../context/AppContext';
import './Topbar.css';

export default function Topbar() {
  const { redisConnection, theme, toggleTheme, disconnect, selectedDb } = useApp();

  return (
    <header className="topbar">
      <div className="topbar-brand">
        <span className="topbar-logo">R</span>
        <span className="topbar-title">Redish Dashboard</span>
      </div>

      <div className="topbar-info">
        {redisConnection && (
          <span className="topbar-server">
            <span className="server-indicator"></span>
            {redisConnection}
            <span className="server-db">DB{selectedDb}</span>
          </span>
        )}
      </div>

      <div className="topbar-actions">
        <button
          className="btn-icon"
          onClick={toggleTheme}
          title={theme === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
        >
          {theme === 'light' ? '🌙' : '☀️'}
        </button>
        {redisConnection && (
          <button className="btn btn-secondary btn-sm" onClick={disconnect}>
            Disconnect
          </button>
        )}
      </div>
    </header>
  );
}
