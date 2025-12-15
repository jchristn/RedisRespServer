import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import { formatNumber } from '../utils/api';
import './Databases.css';

export default function Databases() {
  const { api, selectedDb, selectDatabase } = useApp();
  const navigate = useNavigate();
  const [databases, setDatabases] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [dbCount, setDbCount] = useState(16);

  const loadDatabases = useCallback(async () => {
    if (!api) return;
    setLoading(true);
    setError(null);

    try {
      // Try to get the configured number of databases
      let configuredDbs = 16;
      try {
        const config = await api.executeCommand('CONFIG GET databases');
        if (Array.isArray(config) && config.length >= 2) {
          configuredDbs = parseInt(config[1]) || 16;
        } else if (config?.databases) {
          configuredDbs = parseInt(config.databases) || 16;
        }
      } catch {
        // Fall back to default if CONFIG is not available
      }
      setDbCount(configuredDbs);

      // Get keyspace info which contains key counts per database
      const info = await api.getInfo('keyspace').catch(() => null);

      // Parse keyspace info - format is like: db0:keys=123,expires=45,avg_ttl=6789
      const keyspaceData = {};
      if (info) {
        const infoStr = typeof info === 'string' ? info : JSON.stringify(info);
        const dbMatches = infoStr.matchAll(/db(\d+):keys=(\d+)(?:,expires=(\d+))?/g);
        for (const match of dbMatches) {
          const dbNum = parseInt(match[1]);
          keyspaceData[dbNum] = {
            keys: parseInt(match[2]) || 0,
            expires: parseInt(match[3]) || 0,
          };
        }

        // Also try parsing if it's already an object
        if (typeof info === 'object') {
          for (const [key, value] of Object.entries(info)) {
            const dbMatch = key.match(/^db(\d+)$/);
            if (dbMatch) {
              const dbNum = parseInt(dbMatch[1]);
              if (typeof value === 'string') {
                const keysMatch = value.match(/keys=(\d+)/);
                const expiresMatch = value.match(/expires=(\d+)/);
                keyspaceData[dbNum] = {
                  keys: keysMatch ? parseInt(keysMatch[1]) : 0,
                  expires: expiresMatch ? parseInt(expiresMatch[1]) : 0,
                };
              } else if (typeof value === 'object') {
                keyspaceData[dbNum] = {
                  keys: value.keys || 0,
                  expires: value.expires || 0,
                };
              }
            }
          }
        }
      }

      // Build database list
      const dbList = [];
      for (let i = 0; i < configuredDbs; i++) {
        dbList.push({
          index: i,
          name: `DB${i}`,
          keys: keyspaceData[i]?.keys || 0,
          expires: keyspaceData[i]?.expires || 0,
          hasData: keyspaceData[i] !== undefined,
        });
      }
      setDatabases(dbList);
    } catch (err) {
      setError(err?.message || String(err) || 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    loadDatabases();
  }, [loadDatabases]);

  const handleSelectDatabase = async (dbIndex) => {
    await selectDatabase(dbIndex);
  };

  const handleSelectAndBrowse = async (dbIndex) => {
    await selectDatabase(dbIndex);
    navigate('/keys');
  };

  const handleFlushDatabase = async (dbIndex) => {
    const dbName = `DB${dbIndex}`;
    if (!confirm(`Are you sure you want to flush ${dbName}? This will delete ALL keys in this database.`)) {
      return;
    }

    try {
      // Select the database first, then flush it
      await api.selectDatabase(dbIndex);
      await api.flushDatabase();
      // Refresh the list
      loadDatabases();
    } catch (err) {
      alert(`Failed to flush database: ${err.message}`);
    }
  };

  const totalKeys = databases.reduce((sum, db) => sum + db.keys, 0);
  const usedDatabases = databases.filter(db => db.keys > 0).length;

  if (loading) {
    return (
      <div className="loading">
        <div className="loading-spinner"></div>
        <span>Loading databases...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="empty-state">
        <span className="empty-state-icon">&#9888;&#65039;</span>
        <h3 className="empty-state-title">Error loading databases</h3>
        <p className="empty-state-description">{error}</p>
        <button className="btn btn-primary mt-4" onClick={loadDatabases}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="databases-view">
      <div className="page-header">
        <h1 className="page-title">Databases</h1>
        <div className="page-actions">
          <button className="btn btn-secondary btn-sm" onClick={loadDatabases}>
            Refresh
          </button>
        </div>
      </div>

      <div className="databases-summary">
        <div className="summary-card">
          <div className="summary-value">{dbCount}</div>
          <div className="summary-label">Configured</div>
        </div>
        <div className="summary-card">
          <div className="summary-value">{usedDatabases}</div>
          <div className="summary-label">In Use</div>
        </div>
        <div className="summary-card">
          <div className="summary-value">{formatNumber(totalKeys)}</div>
          <div className="summary-label">Total Keys</div>
        </div>
      </div>

      <div className="databases-info">
        <p className="text-muted">
          Redis databases are numbered namespaces (0 to {dbCount - 1}).
          They cannot be created or deleted dynamically - the count is configured at server startup.
        </p>
      </div>

      <div className="databases-grid">
        {databases.map((db) => (
          <div
            key={db.index}
            className={`database-card ${selectedDb === db.index ? 'selected' : ''} ${db.keys > 0 ? 'has-data' : ''}`}
          >
            <div className="database-header">
              <span className="database-name">{db.name}</span>
              {selectedDb === db.index && (
                <span className="database-badge">Current</span>
              )}
            </div>

            <div className="database-stats">
              <div className="database-stat">
                <span className="stat-value">{formatNumber(db.keys)}</span>
                <span className="stat-label">keys</span>
              </div>
              {db.expires > 0 && (
                <div className="database-stat">
                  <span className="stat-value">{formatNumber(db.expires)}</span>
                  <span className="stat-label">expiring</span>
                </div>
              )}
            </div>

            <div className="database-actions">
              {selectedDb !== db.index ? (
                <button
                  className="btn btn-primary btn-sm"
                  onClick={() => handleSelectDatabase(db.index)}
                >
                  Select
                </button>
              ) : (
                <button
                  className="btn btn-secondary btn-sm"
                  onClick={() => handleSelectAndBrowse(db.index)}
                >
                  Browse Keys
                </button>
              )}
              {db.keys > 0 && (
                <button
                  className="btn btn-danger btn-sm"
                  onClick={() => handleFlushDatabase(db.index)}
                >
                  Flush
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
