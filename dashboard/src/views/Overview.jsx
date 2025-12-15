import { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { formatBytes, formatUptime, formatNumber, parseRedisInfo } from '../utils/api';
import './Overview.css';

export default function Overview() {
  const { api, serverInfo } = useApp();
  const [info, setInfo] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadData = useCallback(async () => {
    if (!api) return;
    try {
      const infoData = await api.getInfo().catch(() => null);

      if (infoData) {
        // Parse info if it's a string (raw Redis INFO format)
        const parsed = typeof infoData === 'string' ? parseRedisInfo(infoData) : infoData;
        setInfo(parsed);
      }
      setError(null);
    } catch (err) {
      setError(err?.message || String(err) || 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    loadData();
    const interval = setInterval(loadData, 5000); // Refresh every 5 seconds
    return () => clearInterval(interval);
  }, [loadData]);

  if (loading) {
    return (
      <div className="loading">
        <div className="loading-spinner"></div>
        <span>Loading server info...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="empty-state">
        <span className="empty-state-icon">⚠️</span>
        <h3 className="empty-state-title">Error loading data</h3>
        <p className="empty-state-description">{error}</p>
        <button className="btn btn-primary mt-4" onClick={loadData}>
          Retry
        </button>
      </div>
    );
  }

  // Extract data from parsed info
  const server = info?.server || {};
  const memory = info?.memory || {};
  const clients = info?.clients || {};
  const statsInfo = info?.stats || {};
  const keyspace = info?.keyspace || {};
  const cpu = info?.cpu || {};

  return (
    <div className="overview">
      <div className="page-header">
        <h1 className="page-title">Server Overview</h1>
        <button className="btn btn-secondary btn-sm" onClick={loadData}>
          Refresh
        </button>
      </div>

      {/* Key Metrics */}
      <div className="metric-grid">
        <div className="metric-card">
          <div className="metric-label">Uptime</div>
          <div className="metric-value">
            {formatUptime(parseInt(server.uptime_in_seconds) || 0)}
          </div>
        </div>
        <div className="metric-card">
          <div className="metric-label">Memory Used</div>
          <div className="metric-value mono">
            {formatBytes(parseInt(memory.used_memory) || 0)}
          </div>
        </div>
        <div className="metric-card">
          <div className="metric-label">Connected Clients</div>
          <div className="metric-value">{clients.connected_clients || '0'}</div>
        </div>
        <div className="metric-card">
          <div className="metric-label">Total Commands</div>
          <div className="metric-value mono">
            {formatNumber(parseInt(statsInfo.total_commands_processed) || 0)}
          </div>
        </div>
      </div>

      {/* Server Information */}
      <div className="info-section">
        <h3 className="info-section-title">Server</h3>
        <div className="info-grid">
          <InfoItem label="Redis Version" value={server.redis_version} />
          <InfoItem label="Server ID" value={server.run_id} truncate />
          <InfoItem label="TCP Port" value={server.tcp_port} />
          <InfoItem label="Process ID" value={server.process_id} />
          <InfoItem label="OS" value={server.os} />
          <InfoItem label="Architecture" value={server.arch_bits ? `${server.arch_bits}-bit` : null} />
          <InfoItem label="Uptime (seconds)" value={server.uptime_in_seconds} />
          <InfoItem label="Uptime (days)" value={server.uptime_in_days} />
        </div>
      </div>

      {/* Memory Information */}
      <div className="info-section">
        <h3 className="info-section-title">Memory</h3>
        <div className="info-grid">
          <InfoItem label="Used Memory" value={formatBytes(parseInt(memory.used_memory) || 0)} />
          <InfoItem label="Used Memory (Human)" value={memory.used_memory_human} />
          <InfoItem label="Peak Memory" value={formatBytes(parseInt(memory.used_memory_peak) || 0)} />
          <InfoItem label="Peak Memory (Human)" value={memory.used_memory_peak_human} />
          <InfoItem label="Total System Memory" value={memory.total_system_memory_human} />
          <InfoItem label="Memory Fragmentation" value={memory.mem_fragmentation_ratio} />
        </div>
      </div>

      {/* CPU Information */}
      {(cpu.used_cpu_sys || cpu.used_cpu_user) && (
        <div className="info-section">
          <h3 className="info-section-title">CPU</h3>
          <div className="info-grid">
            <InfoItem label="System CPU" value={cpu.used_cpu_sys} />
            <InfoItem label="User CPU" value={cpu.used_cpu_user} />
            <InfoItem label="System CPU (Children)" value={cpu.used_cpu_sys_children} />
            <InfoItem label="User CPU (Children)" value={cpu.used_cpu_user_children} />
          </div>
        </div>
      )}

      {/* Stats Information */}
      <div className="info-section">
        <h3 className="info-section-title">Stats</h3>
        <div className="info-grid">
          <InfoItem label="Total Connections" value={formatNumber(parseInt(statsInfo.total_connections_received) || 0)} />
          <InfoItem label="Total Commands" value={formatNumber(parseInt(statsInfo.total_commands_processed) || 0)} />
          <InfoItem label="Ops/sec" value={statsInfo.instantaneous_ops_per_sec} />
          <InfoItem label="Expired Keys" value={statsInfo.expired_keys} />
          <InfoItem label="Evicted Keys" value={statsInfo.evicted_keys} />
          <InfoItem label="Keyspace Hits" value={statsInfo.keyspace_hits} />
          <InfoItem label="Keyspace Misses" value={statsInfo.keyspace_misses} />
        </div>
      </div>

      {/* Keyspace Information */}
      {Object.keys(keyspace).length > 0 && (
        <div className="info-section">
          <h3 className="info-section-title">Keyspace</h3>
          <div className="keyspace-list">
            {Object.entries(keyspace).map(([db, value]) => (
              <div key={db} className="keyspace-item">
                <span className="keyspace-db">{db}</span>
                <span className="keyspace-value">{value}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Raw Info (collapsible) */}
      {serverInfo && (
        <details className="raw-info-details">
          <summary className="raw-info-summary">Raw Server Info</summary>
          <pre className="code-block">
            {typeof serverInfo === 'string' ? serverInfo : JSON.stringify(serverInfo, null, 2)}
          </pre>
        </details>
      )}
    </div>
  );
}

function InfoItem({ label, value, truncate = false }) {
  const displayValue = value ?? 'N/A';
  return (
    <div className="info-item">
      <span className="info-key">{label}</span>
      <span className={`info-value ${truncate ? 'truncate' : ''}`} title={displayValue}>
        {displayValue}
      </span>
    </div>
  );
}
