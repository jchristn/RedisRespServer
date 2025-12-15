/**
 * Redis Dashboard API Client
 *
 * This client communicates with the local Node.js backend server
 * which proxies requests to the actual Redis/Redish server.
 */

export class RedisApi {
  constructor(redisConnection) {
    // redisConnection is the Redis server connection string (e.g., localhost:6379)
    this.redisConnection = redisConnection;
  }

  async request(endpoint, options = {}) {
    const url = `/api${endpoint}`;
    const config = {
      headers: {
        'Content-Type': 'application/json',
        'X-Redis-Connection': this.redisConnection,
        ...options.headers,
      },
      ...options,
    };

    try {
      const response = await fetch(url, config);

      if (response.status === 204) {
        return null;
      }

      const data = await response.json();

      // Handle error responses
      if (data && typeof data === 'object' && data.error) {
        throw new Error(data.error);
      }

      return data;
    } catch (error) {
      if (error.message === 'Failed to fetch') {
        throw new Error('Unable to connect to dashboard server');
      }
      throw error;
    }
  }

  // Health check / connection test
  async ping() {
    return this.request('/ping');
  }

  // Server Info
  async getInfo(section = null) {
    const endpoint = section ? `/info?section=${section}` : '/info';
    return this.request(endpoint);
  }

  async getServerStats() {
    return this.request('/stats');
  }

  // Key Operations
  async getKeys(pattern = '*', cursor = 0, count = 100) {
    return this.request(`/keys?pattern=${encodeURIComponent(pattern)}&cursor=${cursor}&count=${count}`);
  }

  async getKeyType(key) {
    return this.request(`/keys/${encodeURIComponent(key)}/type`);
  }

  async getKeyTTL(key) {
    return this.request(`/keys/${encodeURIComponent(key)}/ttl`);
  }

  async deleteKey(key) {
    return this.request(`/keys/${encodeURIComponent(key)}`, { method: 'DELETE' });
  }

  async setKeyTTL(key, seconds) {
    return this.request(`/keys/${encodeURIComponent(key)}/ttl`, {
      method: 'PUT',
      body: JSON.stringify({ seconds }),
    });
  }

  async removeKeyTTL(key) {
    return this.request(`/keys/${encodeURIComponent(key)}/ttl`, { method: 'DELETE' });
  }

  async renameKey(oldKey, newKey) {
    return this.request(`/keys/${encodeURIComponent(oldKey)}/rename`, {
      method: 'POST',
      body: JSON.stringify({ newKey }),
    });
  }

  // String Operations
  async getString(key) {
    return this.request(`/strings/${encodeURIComponent(key)}`);
  }

  async setString(key, value, ttl = null) {
    return this.request(`/strings/${encodeURIComponent(key)}`, {
      method: 'PUT',
      body: JSON.stringify({ value, ttl }),
    });
  }

  // Hash Operations
  async getHash(key) {
    return this.request(`/hashes/${encodeURIComponent(key)}`);
  }

  async setHashField(key, field, value) {
    return this.request(`/hashes/${encodeURIComponent(key)}`, {
      method: 'PUT',
      body: JSON.stringify({ field, value }),
    });
  }

  async deleteHashField(key, field) {
    return this.request(`/hashes/${encodeURIComponent(key)}/${encodeURIComponent(field)}`, {
      method: 'DELETE',
    });
  }

  // List Operations
  async getList(key, start = 0, stop = -1) {
    return this.request(`/lists/${encodeURIComponent(key)}?start=${start}&stop=${stop}`);
  }

  async pushToList(key, values, position = 'right') {
    return this.request(`/lists/${encodeURIComponent(key)}`, {
      method: 'POST',
      body: JSON.stringify({ values, position }),
    });
  }

  async popFromList(key, position = 'right') {
    return this.request(`/lists/${encodeURIComponent(key)}/pop?position=${position}`, {
      method: 'POST',
    });
  }

  // Set Operations
  async getSet(key) {
    return this.request(`/sets/${encodeURIComponent(key)}`);
  }

  async addToSet(key, members) {
    return this.request(`/sets/${encodeURIComponent(key)}`, {
      method: 'POST',
      body: JSON.stringify({ members }),
    });
  }

  async removeFromSet(key, members) {
    return this.request(`/sets/${encodeURIComponent(key)}/remove`, {
      method: 'POST',
      body: JSON.stringify({ members }),
    });
  }

  // Sorted Set Operations
  async getSortedSet(key, start = 0, stop = -1, withScores = true) {
    return this.request(`/zsets/${encodeURIComponent(key)}?start=${start}&stop=${stop}&withScores=${withScores}`);
  }

  async addToSortedSet(key, members) {
    // members is array of { member, score }
    return this.request(`/zsets/${encodeURIComponent(key)}`, {
      method: 'POST',
      body: JSON.stringify({ members }),
    });
  }

  async removeFromSortedSet(key, members) {
    return this.request(`/zsets/${encodeURIComponent(key)}/remove`, {
      method: 'POST',
      body: JSON.stringify({ members }),
    });
  }

  // Stream Operations
  async getStream(key, start = '-', end = '+', count = 100) {
    return this.request(`/streams/${encodeURIComponent(key)}?start=${start}&end=${end}&count=${count}`);
  }

  async addToStream(key, fields, id = '*') {
    return this.request(`/streams/${encodeURIComponent(key)}`, {
      method: 'POST',
      body: JSON.stringify({ id, fields }),
    });
  }

  async deleteFromStream(key, ids) {
    return this.request(`/streams/${encodeURIComponent(key)}/delete`, {
      method: 'POST',
      body: JSON.stringify({ ids }),
    });
  }

  async getStreamInfo(key) {
    return this.request(`/streams/${encodeURIComponent(key)}/info`);
  }

  // JSON Operations
  async getJson(key, path = '.') {
    return this.request(`/json/${encodeURIComponent(key)}?path=${encodeURIComponent(path)}`);
  }

  async setJson(key, value, path = '.') {
    return this.request(`/json/${encodeURIComponent(key)}`, {
      method: 'PUT',
      body: JSON.stringify({ path, value }),
    });
  }

  async deleteJson(key, path = '.') {
    return this.request(`/json/${encodeURIComponent(key)}?path=${encodeURIComponent(path)}`, {
      method: 'DELETE',
    });
  }

  // Database Operations
  async selectDatabase(db) {
    return this.request(`/database/select`, {
      method: 'POST',
      body: JSON.stringify({ db }),
    });
  }

  async getDatabaseSize() {
    return this.request('/database/size');
  }

  async flushDatabase() {
    return this.request('/database/flush', { method: 'POST' });
  }

  // Client Operations
  async getClients() {
    return this.request('/clients');
  }

  // CLI / Raw Command Execution
  async executeCommand(command) {
    return this.request('/command', {
      method: 'POST',
      body: JSON.stringify({ command }),
    });
  }

  // Get key value by type (convenience method)
  async getKeyValue(key, type) {
    switch (type?.toLowerCase()) {
      case 'string':
        return this.getString(key);
      case 'hash':
        return this.getHash(key);
      case 'list':
        return this.getList(key);
      case 'set':
        return this.getSet(key);
      case 'zset':
        return this.getSortedSet(key);
      case 'stream':
        return this.getStream(key);
      case 'json':
        return this.getJson(key);
      default:
        throw new Error(`Unknown type: ${type}`);
    }
  }
}

// Utility functions
export function formatBytes(bytes) {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

export function formatNumber(num) {
  if (num === null || num === undefined) return 'N/A';
  return num.toLocaleString();
}

export function formatUptime(seconds) {
  if (!seconds) return 'N/A';
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;

  const parts = [];
  if (days > 0) parts.push(`${days}d`);
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  if (secs > 0 || parts.length === 0) parts.push(`${secs}s`);

  return parts.join(' ');
}

export function formatDate(date) {
  if (!date) return 'N/A';
  return new Date(date).toLocaleString();
}

export function getTypeColor(type) {
  const colors = {
    string: 'type-string',
    hash: 'type-hash',
    list: 'type-list',
    set: 'type-set',
    zset: 'type-zset',
    stream: 'type-stream',
    json: 'type-json',
    none: 'type-none',
  };
  return colors[type?.toLowerCase()] || 'type-none';
}

export function parseRedisInfo(infoString) {
  if (!infoString) return {};

  const sections = {};
  let currentSection = 'default';

  infoString.split('\n').forEach(line => {
    line = line.trim();
    if (!line) return;

    if (line.startsWith('#')) {
      currentSection = line.substring(1).trim().toLowerCase();
      sections[currentSection] = {};
    } else if (line.includes(':')) {
      const [key, value] = line.split(':');
      if (!sections[currentSection]) sections[currentSection] = {};
      sections[currentSection][key.trim()] = value.trim();
    }
  });

  return sections;
}
