import { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { formatDate } from '../utils/api';
import './Clients.css';

export default function Clients() {
  const { api } = useApp();
  const [clients, setClients] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadClients = useCallback(async () => {
    if (!api) return;
    try {
      const data = await api.getClients();
      // Handle different response formats
      const clientList = Array.isArray(data) ? data : data?.clients || [];
      setClients(clientList);
      setError(null);
    } catch (err) {
      setError(err?.message || String(err) || 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    loadClients();
    const interval = setInterval(loadClients, 5000);
    return () => clearInterval(interval);
  }, [loadClients]);

  if (loading) {
    return (
      <div className="loading">
        <div className="loading-spinner"></div>
        <span>Loading clients...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="empty-state">
        <span className="empty-state-icon">⚠️</span>
        <h3 className="empty-state-title">Error loading clients</h3>
        <p className="empty-state-description">{error}</p>
        <button className="btn btn-primary mt-4" onClick={loadClients}>
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="clients-view">
      <div className="page-header">
        <h1 className="page-title">Connected Clients</h1>
        <div className="page-actions">
          <span className="client-count">{clients.length} connected</span>
          <button className="btn btn-secondary btn-sm" onClick={loadClients}>
            Refresh
          </button>
        </div>
      </div>

      {clients.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">👥</span>
          <h3 className="empty-state-title">No connected clients</h3>
          <p className="empty-state-description">
            There are currently no clients connected to the server.
          </p>
        </div>
      ) : (
        <div className="clients-grid">
          {clients.map((client, index) => (
            <ClientCard key={client.id || client.clientId || index} client={client} />
          ))}
        </div>
      )}
    </div>
  );
}

function ClientCard({ client }) {
  return (
    <div className="client-card">
      <div className="client-header">
        <div className="client-id">
          <span className="client-id-label">Client ID</span>
          <span className="client-id-value mono">{client.id || client.clientId || 'N/A'}</span>
        </div>
        <span className="status-badge status-connected">Connected</span>
      </div>

      <div className="client-details">
        <ClientDetail label="Name" value={client.name || '-'} />
        <ClientDetail label="Address" value={client.addr || client.address || '-'} mono />
        <ClientDetail label="Library" value={client.libraryName || client.lib || '-'} />
        <ClientDetail label="Version" value={client.libraryVersion || client.libVer || '-'} />
        <ClientDetail label="RESP Version" value={client.respVersion || client.resp || '-'} />
        <ClientDetail
          label="Connected"
          value={
            client.connectedAt || client.connected_at
              ? formatDate(client.connectedAt || client.connected_at)
              : '-'
          }
        />
        <ClientDetail label="Age (sec)" value={client.age || '-'} />
        <ClientDetail label="Idle (sec)" value={client.idle || '-'} />
        <ClientDetail label="Database" value={client.db ?? '-'} />
        <ClientDetail label="Subscriptions" value={client.sub || '-'} />
        <ClientDetail label="Commands" value={client.cmd || client.lastCommand || '-'} />
      </div>

      {client.flags && (
        <div className="client-flags">
          <span className="flags-label">Flags:</span>
          <span className="flags-value mono">{client.flags}</span>
        </div>
      )}
    </div>
  );
}

function ClientDetail({ label, value, mono = false }) {
  return (
    <div className="client-detail">
      <span className="detail-label">{label}</span>
      <span className={`detail-value ${mono ? 'mono' : ''}`}>{value}</span>
    </div>
  );
}
