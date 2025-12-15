import { useState, useEffect, useMemo, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { getTypeColor } from '../utils/api';
import Modal from '../components/Modal';
import CopyableText from '../components/CopyableText';
import KeyDetail from './KeyDetail';
import './Keys.css';

export default function Keys() {
  const { api, selectedDb, selectDatabase } = useApp();
  const [keys, setKeys] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [pattern, setPattern] = useState('*');
  const [searchInput, setSearchInput] = useState('*');
  const [dbSize, setDbSize] = useState(0);
  const [selectedKey, setSelectedKey] = useState(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [newKey, setNewKey] = useState({ key: '', type: 'string', value: '' });
  const [keyTypes, setKeyTypes] = useState({});

  const loadKeys = useCallback(async () => {
    if (!api) return;
    setLoading(true);
    setError(null);

    try {
      const [keysData, sizeData] = await Promise.all([
        api.getKeys(pattern),
        api.getDatabaseSize().catch(() => ({ size: 0 })),
      ]);

      const keyList = Array.isArray(keysData) ? keysData : keysData?.keys || [];
      setKeys(keyList);
      // Handle different response formats for database size
      const size = typeof sizeData === 'object' ? sizeData?.size : sizeData;
      setDbSize(size ?? 0);

      // Load types for each key (in batches)
      if (keyList.length > 0 && keyList.length <= 100) {
        const types = {};
        await Promise.all(
          keyList.map(async (key) => {
            try {
              const type = await api.getKeyType(key);
              types[key] = type?.type || type || 'unknown';
            } catch {
              types[key] = 'unknown';
            }
          })
        );
        setKeyTypes(types);
      }
    } catch (err) {
      setError(err?.message || String(err) || 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [api, pattern]);

  useEffect(() => {
    loadKeys();
  }, [loadKeys]);

  const handleSearch = (e) => {
    e.preventDefault();
    setPattern(searchInput || '*');
  };

  const handleDeleteKey = async (key) => {
    if (!confirm(`Are you sure you want to delete "${key}"?`)) return;

    try {
      await api.deleteKey(key);
      setKeys(keys.filter((k) => k !== key));
      if (selectedKey === key) setSelectedKey(null);
    } catch (err) {
      alert(`Failed to delete key: ${err.message}`);
    }
  };

  const handleAddKey = async (e) => {
    e.preventDefault();
    if (!newKey.key.trim()) return;

    try {
      switch (newKey.type) {
        case 'string':
          await api.setString(newKey.key, newKey.value);
          break;
        case 'hash':
          await api.setHashField(newKey.key, 'field1', newKey.value || 'value');
          break;
        case 'list':
          await api.pushToList(newKey.key, [newKey.value || 'item']);
          break;
        case 'set':
          await api.addToSet(newKey.key, [newKey.value || 'member']);
          break;
        case 'zset':
          await api.addToSortedSet(newKey.key, [{ member: newKey.value || 'member', score: 0 }]);
          break;
        default:
          await api.setString(newKey.key, newKey.value);
      }
      setShowAddModal(false);
      setNewKey({ key: '', type: 'string', value: '' });
      loadKeys();
    } catch (err) {
      alert(`Failed to create key: ${err.message}`);
    }
  };

  const filteredKeys = useMemo(() => {
    return keys;
  }, [keys]);

  return (
    <div className="keys-view">
      <div className="page-header">
        <h1 className="page-title">Keys</h1>
        <div className="page-actions">
          <span className="key-count">{dbSize} total keys</span>
          <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
            + Add Key
          </button>
        </div>
      </div>

      {/* Database selector and search */}
      <div className="keys-toolbar">
        <div className="db-selector">
          <label className="form-label">Database:</label>
          <select
            className="select db-select"
            value={selectedDb}
            onChange={(e) => selectDatabase(parseInt(e.target.value))}
          >
            {[...Array(16)].map((_, i) => (
              <option key={i} value={i}>
                DB{i}
              </option>
            ))}
          </select>
        </div>

        <form className="search-form" onSubmit={handleSearch}>
          <input
            type="text"
            className="input search-input"
            placeholder="Search pattern (e.g., user:*, *:session)"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <button type="submit" className="btn btn-secondary">
            Search
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => {
              setSearchInput('*');
              setPattern('*');
            }}
          >
            Reset
          </button>
        </form>
      </div>

      {/* Keys list */}
      <div className="keys-content">
        <div className="keys-list-container">
          {loading ? (
            <div className="loading">
              <div className="loading-spinner"></div>
              <span>Loading keys...</span>
            </div>
          ) : error ? (
            <div className="empty-state">
              <span className="empty-state-icon">⚠️</span>
              <h3 className="empty-state-title">Error</h3>
              <p className="empty-state-description">{error}</p>
              <button className="btn btn-primary mt-4" onClick={loadKeys}>
                Retry
              </button>
            </div>
          ) : filteredKeys.length === 0 ? (
            <div className="empty-state">
              <span className="empty-state-icon">🔑</span>
              <h3 className="empty-state-title">No keys found</h3>
              <p className="empty-state-description">
                {pattern === '*'
                  ? 'This database is empty. Add a key to get started.'
                  : `No keys match the pattern "${pattern}"`}
              </p>
            </div>
          ) : (
            <table className="table keys-table">
              <thead>
                <tr>
                  <th>Key</th>
                  <th style={{ width: '100px' }}>Type</th>
                  <th style={{ width: '120px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredKeys.map((key) => (
                  <tr
                    key={key}
                    className={selectedKey === key ? 'selected' : ''}
                    onClick={() => setSelectedKey(key)}
                  >
                    <td>
                      <CopyableText text={key} truncate />
                    </td>
                    <td>
                      <span className={`type-badge ${getTypeColor(keyTypes[key])}`}>
                        {keyTypes[key] || '...'}
                      </span>
                    </td>
                    <td>
                      <div className="key-actions">
                        <button
                          className="btn btn-sm btn-secondary"
                          onClick={(e) => {
                            e.stopPropagation();
                            setSelectedKey(key);
                          }}
                        >
                          View
                        </button>
                        <button
                          className="btn btn-sm btn-danger"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDeleteKey(key);
                          }}
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Key detail panel */}
        {selectedKey && (
          <div className="key-detail-panel">
            <KeyDetail
              keyName={selectedKey}
              keyType={keyTypes[selectedKey]}
              onClose={() => setSelectedKey(null)}
              onDelete={() => handleDeleteKey(selectedKey)}
              onRefresh={loadKeys}
            />
          </div>
        )}
      </div>

      {/* Add Key Modal */}
      <Modal isOpen={showAddModal} onClose={() => setShowAddModal(false)} title="Add New Key">
        <form onSubmit={handleAddKey}>
          <div className="form-group">
            <label className="form-label">Key Name</label>
            <input
              type="text"
              className="input"
              placeholder="Enter key name"
              value={newKey.key}
              onChange={(e) => setNewKey({ ...newKey, key: e.target.value })}
              required
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="form-label">Type</label>
            <select
              className="select"
              value={newKey.type}
              onChange={(e) => setNewKey({ ...newKey, type: e.target.value })}
            >
              <option value="string">String</option>
              <option value="hash">Hash</option>
              <option value="list">List</option>
              <option value="set">Set</option>
              <option value="zset">Sorted Set</option>
            </select>
          </div>

          <div className="form-group">
            <label className="form-label">
              {newKey.type === 'string'
                ? 'Value'
                : newKey.type === 'hash'
                ? 'Initial Field Value'
                : newKey.type === 'list'
                ? 'First Item'
                : newKey.type === 'set'
                ? 'First Member'
                : 'First Member'}
            </label>
            <input
              type="text"
              className="input"
              placeholder="Enter value"
              value={newKey.value}
              onChange={(e) => setNewKey({ ...newKey, value: e.target.value })}
            />
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={() => setShowAddModal(false)}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary">
              Create Key
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
