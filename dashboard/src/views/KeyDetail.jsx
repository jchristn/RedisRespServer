import { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { getTypeColor, formatUptime } from '../utils/api';
import './KeyDetail.css';

export default function KeyDetail({ keyName, keyType, onClose, onDelete, onRefresh }) {
  const { api } = useApp();
  const [value, setValue] = useState(null);
  const [ttl, setTtl] = useState(-1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [editMode, setEditMode] = useState(false);
  const [editValue, setEditValue] = useState('');
  const [newTtl, setNewTtl] = useState('');

  const loadValue = useCallback(async () => {
    if (!api || !keyName) return;
    setLoading(true);
    setError(null);

    try {
      const [valueData, ttlData] = await Promise.all([
        api.getKeyValue(keyName, keyType),
        api.getKeyTTL(keyName).catch(() => -1),
      ]);

      setValue(valueData);
      setTtl(ttlData?.ttl ?? ttlData ?? -1);
      setEditValue(typeof valueData === 'string' ? valueData : JSON.stringify(valueData, null, 2));
    } catch (err) {
      setError(err?.message || String(err) || 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [api, keyName, keyType]);

  useEffect(() => {
    loadValue();
  }, [loadValue]);

  const handleSave = async () => {
    try {
      if (keyType === 'string') {
        await api.setString(keyName, editValue);
      } else if (keyType === 'json') {
        await api.setJson(keyName, JSON.parse(editValue));
      }
      setEditMode(false);
      loadValue();
      onRefresh?.();
    } catch (err) {
      alert(`Failed to save: ${err.message}`);
    }
  };

  const handleSetTTL = async () => {
    if (!newTtl || isNaN(parseInt(newTtl))) return;
    try {
      await api.setKeyTTL(keyName, parseInt(newTtl));
      setNewTtl('');
      loadValue();
    } catch (err) {
      alert(`Failed to set TTL: ${err.message}`);
    }
  };

  const handleRemoveTTL = async () => {
    try {
      await api.removeKeyTTL(keyName);
      loadValue();
    } catch (err) {
      alert(`Failed to remove TTL: ${err.message}`);
    }
  };

  const renderValue = () => {
    if (loading) {
      return (
        <div className="loading">
          <div className="loading-spinner"></div>
        </div>
      );
    }

    if (error) {
      return <div className="text-danger">{error}</div>;
    }

    if (value === null || value === undefined) {
      return <div className="text-muted">No value</div>;
    }

    switch (keyType?.toLowerCase()) {
      case 'string':
        return editMode ? (
          <div className="edit-string">
            <textarea
              className="textarea code-block"
              value={editValue}
              onChange={(e) => setEditValue(e.target.value)}
              rows={10}
            />
            <div className="edit-actions">
              <button className="btn btn-primary btn-sm" onClick={handleSave}>
                Save
              </button>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => {
                  setEditMode(false);
                  setEditValue(value);
                }}
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <div className="string-value">
            <pre className="code-block">{value}</pre>
            <button className="btn btn-secondary btn-sm mt-2" onClick={() => setEditMode(true)}>
              Edit
            </button>
          </div>
        );

      case 'hash':
        return <HashValue value={value} keyName={keyName} api={api} onRefresh={loadValue} />;

      case 'list':
        return <ListValue value={value} keyName={keyName} api={api} onRefresh={loadValue} />;

      case 'set':
        return <SetValue value={value} keyName={keyName} api={api} onRefresh={loadValue} />;

      case 'zset':
        return <SortedSetValue value={value} keyName={keyName} api={api} onRefresh={loadValue} />;

      case 'stream':
        return <StreamValue value={value} />;

      case 'json':
        return editMode ? (
          <div className="edit-json">
            <textarea
              className="textarea code-block"
              value={editValue}
              onChange={(e) => setEditValue(e.target.value)}
              rows={15}
            />
            <div className="edit-actions">
              <button className="btn btn-primary btn-sm" onClick={handleSave}>
                Save
              </button>
              <button
                className="btn btn-secondary btn-sm"
                onClick={() => {
                  setEditMode(false);
                  setEditValue(JSON.stringify(value, null, 2));
                }}
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <div className="json-value">
            <pre className="code-block">{JSON.stringify(value, null, 2)}</pre>
            <button className="btn btn-secondary btn-sm mt-2" onClick={() => setEditMode(true)}>
              Edit
            </button>
          </div>
        );

      default:
        return (
          <pre className="code-block">
            {typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value)}
          </pre>
        );
    }
  };

  return (
    <div className="key-detail">
      <div className="key-detail-header">
        <div className="key-detail-title">
          <span className={`type-badge ${getTypeColor(keyType)}`}>{keyType || 'unknown'}</span>
          <h3 className="key-name mono">{keyName}</h3>
        </div>
        <button className="btn-icon" onClick={onClose} title="Close">
          &times;
        </button>
      </div>

      <div className="key-detail-meta">
        <div className="ttl-info">
          <span className="ttl-label">TTL:</span>
          {ttl === -1 ? (
            <span className="ttl-value">No expiration</span>
          ) : ttl === -2 ? (
            <span className="ttl-value text-danger">Key does not exist</span>
          ) : (
            <span className="ttl-value">{formatUptime(ttl)}</span>
          )}
        </div>

        <div className="ttl-actions">
          <input
            type="number"
            className="input ttl-input"
            placeholder="Seconds"
            value={newTtl}
            onChange={(e) => setNewTtl(e.target.value)}
            min="1"
          />
          <button className="btn btn-secondary btn-sm" onClick={handleSetTTL} disabled={!newTtl}>
            Set TTL
          </button>
          {ttl > 0 && (
            <button className="btn btn-secondary btn-sm" onClick={handleRemoveTTL}>
              Remove TTL
            </button>
          )}
        </div>
      </div>

      <div className="key-detail-value">{renderValue()}</div>

      <div className="key-detail-footer">
        <button className="btn btn-secondary btn-sm" onClick={loadValue}>
          Refresh
        </button>
        <button className="btn btn-danger btn-sm" onClick={onDelete}>
          Delete Key
        </button>
      </div>
    </div>
  );
}

// Hash Value Component
function HashValue({ value, keyName, api, onRefresh }) {
  const [newField, setNewField] = useState({ key: '', value: '' });

  const entries = value
    ? Object.entries(value).map(([k, v]) => ({ field: k, value: v }))
    : [];

  const handleAddField = async () => {
    if (!newField.key.trim()) return;
    try {
      await api.setHashField(keyName, newField.key, newField.value);
      setNewField({ key: '', value: '' });
      onRefresh();
    } catch (err) {
      alert(`Failed to add field: ${err.message}`);
    }
  };

  const handleDeleteField = async (field) => {
    try {
      await api.deleteHashField(keyName, field);
      onRefresh();
    } catch (err) {
      alert(`Failed to delete field: ${err.message}`);
    }
  };

  return (
    <div className="hash-value">
      <table className="table">
        <thead>
          <tr>
            <th>Field</th>
            <th>Value</th>
            <th style={{ width: '60px' }}></th>
          </tr>
        </thead>
        <tbody>
          {entries.map(({ field, value }) => (
            <tr key={field}>
              <td className="mono">{field}</td>
              <td className="mono truncate">{value}</td>
              <td>
                <button className="btn-icon text-danger" onClick={() => handleDeleteField(field)}>
                  &times;
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="add-field-form">
        <input
          type="text"
          className="input"
          placeholder="Field"
          value={newField.key}
          onChange={(e) => setNewField({ ...newField, key: e.target.value })}
        />
        <input
          type="text"
          className="input"
          placeholder="Value"
          value={newField.value}
          onChange={(e) => setNewField({ ...newField, value: e.target.value })}
        />
        <button className="btn btn-primary btn-sm" onClick={handleAddField}>
          Add
        </button>
      </div>
    </div>
  );
}

// List Value Component
function ListValue({ value, keyName, api, onRefresh }) {
  const [newItem, setNewItem] = useState('');
  const items = Array.isArray(value) ? value : [];

  const handlePush = async (position) => {
    if (!newItem.trim()) return;
    try {
      await api.pushToList(keyName, [newItem], position);
      setNewItem('');
      onRefresh();
    } catch (err) {
      alert(`Failed to push: ${err.message}`);
    }
  };

  return (
    <div className="list-value">
      <div className="list-items">
        {items.map((item, index) => (
          <div key={index} className="list-item">
            <span className="list-index">{index}</span>
            <span className="list-item-value mono">{item}</span>
          </div>
        ))}
      </div>

      <div className="add-item-form">
        <input
          type="text"
          className="input"
          placeholder="New item"
          value={newItem}
          onChange={(e) => setNewItem(e.target.value)}
        />
        <button className="btn btn-secondary btn-sm" onClick={() => handlePush('left')}>
          Push Left
        </button>
        <button className="btn btn-primary btn-sm" onClick={() => handlePush('right')}>
          Push Right
        </button>
      </div>
    </div>
  );
}

// Set Value Component
function SetValue({ value, keyName, api, onRefresh }) {
  const [newMember, setNewMember] = useState('');
  const members = Array.isArray(value) ? value : [];

  const handleAdd = async () => {
    if (!newMember.trim()) return;
    try {
      await api.addToSet(keyName, [newMember]);
      setNewMember('');
      onRefresh();
    } catch (err) {
      alert(`Failed to add member: ${err.message}`);
    }
  };

  const handleRemove = async (member) => {
    try {
      await api.removeFromSet(keyName, [member]);
      onRefresh();
    } catch (err) {
      alert(`Failed to remove member: ${err.message}`);
    }
  };

  return (
    <div className="set-value">
      <div className="set-members">
        {members.map((member, index) => (
          <div key={index} className="set-member">
            <span className="mono">{member}</span>
            <button className="btn-icon text-danger" onClick={() => handleRemove(member)}>
              &times;
            </button>
          </div>
        ))}
      </div>

      <div className="add-member-form">
        <input
          type="text"
          className="input"
          placeholder="New member"
          value={newMember}
          onChange={(e) => setNewMember(e.target.value)}
        />
        <button className="btn btn-primary btn-sm" onClick={handleAdd}>
          Add Member
        </button>
      </div>
    </div>
  );
}

// Sorted Set Value Component
function SortedSetValue({ value, keyName, api, onRefresh }) {
  const [newMember, setNewMember] = useState({ member: '', score: '0' });

  // value could be array of [member, score] pairs or object
  const entries = Array.isArray(value)
    ? value.map((item, i) =>
        Array.isArray(item)
          ? { member: item[0], score: item[1] }
          : typeof item === 'object'
          ? item
          : { member: item, score: i }
      )
    : [];

  const handleAdd = async () => {
    if (!newMember.member.trim()) return;
    try {
      await api.addToSortedSet(keyName, [
        { member: newMember.member, score: parseFloat(newMember.score) || 0 },
      ]);
      setNewMember({ member: '', score: '0' });
      onRefresh();
    } catch (err) {
      alert(`Failed to add member: ${err.message}`);
    }
  };

  const handleRemove = async (member) => {
    try {
      await api.removeFromSortedSet(keyName, [member]);
      onRefresh();
    } catch (err) {
      alert(`Failed to remove member: ${err.message}`);
    }
  };

  return (
    <div className="zset-value">
      <table className="table">
        <thead>
          <tr>
            <th>Score</th>
            <th>Member</th>
            <th style={{ width: '60px' }}></th>
          </tr>
        </thead>
        <tbody>
          {entries.map(({ member, score }, index) => (
            <tr key={index}>
              <td className="mono">{score}</td>
              <td className="mono truncate">{member}</td>
              <td>
                <button className="btn-icon text-danger" onClick={() => handleRemove(member)}>
                  &times;
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="add-zset-form">
        <input
          type="number"
          className="input score-input"
          placeholder="Score"
          value={newMember.score}
          onChange={(e) => setNewMember({ ...newMember, score: e.target.value })}
        />
        <input
          type="text"
          className="input"
          placeholder="Member"
          value={newMember.member}
          onChange={(e) => setNewMember({ ...newMember, member: e.target.value })}
        />
        <button className="btn btn-primary btn-sm" onClick={handleAdd}>
          Add
        </button>
      </div>
    </div>
  );
}

// Stream Value Component
function StreamValue({ value }) {
  const entries = Array.isArray(value) ? value : [];

  return (
    <div className="stream-value">
      {entries.length === 0 ? (
        <div className="text-muted">No entries</div>
      ) : (
        <div className="stream-entries">
          {entries.map((entry, index) => (
            <div key={index} className="stream-entry">
              <div className="stream-entry-id mono">
                {entry.id || entry[0] || `Entry ${index}`}
              </div>
              <div className="stream-entry-fields">
                {typeof entry === 'object' &&
                  Object.entries(entry.fields || entry[1] || {}).map(([field, val]) => (
                    <div key={field} className="stream-field">
                      <span className="field-name">{field}:</span>
                      <span className="field-value mono">{val}</span>
                    </div>
                  ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
