import express from 'express';
import cors from 'cors';
import Redis from 'ioredis';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import { existsSync } from 'fs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const app = express();
const PORT = process.env.PORT || 3002;

app.use(cors());
app.use(express.json());

// Serve static files from dist directory in production
const distPath = join(__dirname, '..', 'dist');
if (existsSync(distPath)) {
  app.use(express.static(distPath));
}

// Store Redis connections per session/connection string
const connections = new Map();

function getRedisClient(connectionString) {
  if (!connectionString) {
    throw new Error('No connection string provided');
  }

  if (connections.has(connectionString)) {
    const client = connections.get(connectionString);
    if (client.status === 'ready') {
      return client;
    }
    // Remove stale connection
    connections.delete(connectionString);
  }

  // Parse connection string (format: redis://host:port or just host:port)
  let host = 'localhost';
  let port = 6379;

  if (connectionString.startsWith('redis://')) {
    const url = new URL(connectionString);
    host = url.hostname;
    port = parseInt(url.port) || 6379;
  } else if (connectionString.includes(':')) {
    const parts = connectionString.split(':');
    host = parts[0];
    port = parseInt(parts[1]) || 6379;
  } else {
    host = connectionString;
  }

  const client = new Redis({
    host,
    port,
    lazyConnect: true,
    retryStrategy: () => null, // Don't auto-retry
  });

  connections.set(connectionString, client);
  return client;
}

// Middleware to get Redis client from header
function withRedis(req, res, next) {
  const connectionString = req.headers['x-redis-connection'];
  if (!connectionString) {
    return res.status(400).json({ error: 'Missing X-Redis-Connection header' });
  }

  try {
    req.redis = getRedisClient(connectionString);
    next();
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
}

// Health check / ping
app.get('/api/ping', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const result = await req.redis.ping();
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Server info
app.get('/api/info', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const section = req.query.section;
    const result = section ? await req.redis.info(section) : await req.redis.info();
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Server stats
app.get('/api/stats', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const info = await req.redis.info();
    res.json({ info });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Database size
app.get('/api/database/size', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const size = await req.redis.dbsize();
    res.json({ size });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Select database
app.post('/api/database/select', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { db } = req.body;
    await req.redis.select(db);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Flush database
app.post('/api/database/flush', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    await req.redis.flushdb();
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Get keys
app.get('/api/keys', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const pattern = req.query.pattern || '*';
    const cursor = req.query.cursor || '0';
    const count = parseInt(req.query.count) || 100;

    // Use SCAN for production safety
    const [newCursor, keys] = await req.redis.scan(cursor, 'MATCH', pattern, 'COUNT', count);
    res.json({ cursor: newCursor, keys });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Get key type
app.get('/api/keys/:key/type', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const type = await req.redis.type(req.params.key);
    res.json({ type });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Get key TTL
app.get('/api/keys/:key/ttl', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const ttl = await req.redis.ttl(req.params.key);
    res.json({ ttl });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Set key TTL
app.put('/api/keys/:key/ttl', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { seconds } = req.body;
    await req.redis.expire(req.params.key, seconds);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Remove key TTL
app.delete('/api/keys/:key/ttl', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    await req.redis.persist(req.params.key);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Delete key
app.delete('/api/keys/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const deleted = await req.redis.del(req.params.key);
    res.json({ deleted });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Rename key
app.post('/api/keys/:key/rename', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { newKey } = req.body;
    await req.redis.rename(req.params.key, newKey);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// String operations
app.get('/api/strings/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const value = await req.redis.get(req.params.key);
    res.json(value);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.put('/api/strings/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { value, ttl } = req.body;
    if (ttl) {
      await req.redis.setex(req.params.key, ttl, value);
    } else {
      await req.redis.set(req.params.key, value);
    }
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Hash operations
app.get('/api/hashes/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const value = await req.redis.hgetall(req.params.key);
    res.json(value);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.put('/api/hashes/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { field, value } = req.body;
    await req.redis.hset(req.params.key, field, value);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.delete('/api/hashes/:key/:field', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const deleted = await req.redis.hdel(req.params.key, req.params.field);
    res.json({ deleted });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// List operations
app.get('/api/lists/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const start = parseInt(req.query.start) || 0;
    const stop = parseInt(req.query.stop) || -1;
    const value = await req.redis.lrange(req.params.key, start, stop);
    res.json(value);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/lists/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { values, position } = req.body;
    let length;
    if (position === 'left') {
      length = await req.redis.lpush(req.params.key, ...values);
    } else {
      length = await req.redis.rpush(req.params.key, ...values);
    }
    res.json({ length });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/lists/:key/pop', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const position = req.query.position || 'right';
    let value;
    if (position === 'left') {
      value = await req.redis.lpop(req.params.key);
    } else {
      value = await req.redis.rpop(req.params.key);
    }
    res.json(value);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Set operations
app.get('/api/sets/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const members = await req.redis.smembers(req.params.key);
    res.json(members);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/sets/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { members } = req.body;
    const added = await req.redis.sadd(req.params.key, ...members);
    res.json({ added });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/sets/:key/remove', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { members } = req.body;
    const removed = await req.redis.srem(req.params.key, ...members);
    res.json({ removed });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Sorted set operations
app.get('/api/zsets/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const start = parseInt(req.query.start) || 0;
    const stop = parseInt(req.query.stop) || -1;
    const withScores = req.query.withScores !== 'false';

    let result;
    if (withScores) {
      result = await req.redis.zrange(req.params.key, start, stop, 'WITHSCORES');
      // Convert to array of {member, score} objects
      const formatted = [];
      for (let i = 0; i < result.length; i += 2) {
        formatted.push({ member: result[i], score: parseFloat(result[i + 1]) });
      }
      res.json(formatted);
    } else {
      result = await req.redis.zrange(req.params.key, start, stop);
      res.json(result);
    }
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/zsets/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { members } = req.body;
    // members is array of {member, score}
    const args = [];
    for (const m of members) {
      args.push(m.score, m.member);
    }
    const added = await req.redis.zadd(req.params.key, ...args);
    res.json({ added });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/zsets/:key/remove', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { members } = req.body;
    const removed = await req.redis.zrem(req.params.key, ...members);
    res.json({ removed });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Stream operations
app.get('/api/streams/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const start = req.query.start || '-';
    const end = req.query.end || '+';
    const count = parseInt(req.query.count) || 100;

    const result = await req.redis.xrange(req.params.key, start, end, 'COUNT', count);
    // Format entries
    const entries = result.map(([id, fields]) => {
      const fieldObj = {};
      for (let i = 0; i < fields.length; i += 2) {
        fieldObj[fields[i]] = fields[i + 1];
      }
      return { id, fields: fieldObj };
    });
    res.json(entries);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/streams/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { id = '*', fields } = req.body;
    const args = [];
    for (const [k, v] of Object.entries(fields)) {
      args.push(k, v);
    }
    const newId = await req.redis.xadd(req.params.key, id, ...args);
    res.json({ id: newId });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.get('/api/streams/:key/info', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const info = await req.redis.xinfo('STREAM', req.params.key);
    res.json(info);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/streams/:key/delete', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { ids } = req.body;
    const deleted = await req.redis.xdel(req.params.key, ...ids);
    res.json({ deleted });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// JSON operations (RedisJSON)
app.get('/api/json/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const path = req.query.path || '.';
    const result = await req.redis.call('JSON.GET', req.params.key, path);
    res.json(result ? JSON.parse(result) : null);
  } catch (err) {
    // If JSON module not available, return null
    res.json(null);
  }
});

app.put('/api/json/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { path = '.', value } = req.body;
    await req.redis.call('JSON.SET', req.params.key, path, JSON.stringify(value));
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

app.delete('/api/json/:key', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const path = req.query.path || '.';
    await req.redis.call('JSON.DEL', req.params.key, path);
    res.json({ success: true });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Client list
app.get('/api/clients', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const result = await req.redis.client('LIST');
    // Parse client list string into objects
    const clients = result.split('\n').filter(Boolean).map(line => {
      const client = {};
      line.split(' ').forEach(pair => {
        const [key, value] = pair.split('=');
        if (key && value !== undefined) {
          client[key] = value;
        }
      });
      return client;
    });
    res.json(clients);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Execute raw command
app.post('/api/command', withRedis, async (req, res) => {
  try {
    await req.redis.connect().catch(() => {});
    const { command } = req.body;

    // Parse command string into parts
    const parts = parseCommand(command);
    if (parts.length === 0) {
      return res.json({ error: 'Empty command' });
    }

    const cmd = parts[0].toLowerCase();
    const args = parts.slice(1);

    const result = await req.redis.call(cmd, ...args);
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

function parseCommand(command) {
  const parts = [];
  let current = '';
  let inQuote = false;
  let quoteChar = '';

  for (const c of command) {
    if (inQuote) {
      if (c === quoteChar) {
        inQuote = false;
      } else {
        current += c;
      }
    } else {
      if (c === '"' || c === "'") {
        inQuote = true;
        quoteChar = c;
      } else if (/\s/.test(c)) {
        if (current) {
          parts.push(current);
          current = '';
        }
      } else {
        current += c;
      }
    }
  }

  if (current) {
    parts.push(current);
  }

  return parts;
}

// Clean up connections on exit
process.on('SIGINT', () => {
  console.log('\nShutting down...');
  for (const client of connections.values()) {
    client.disconnect();
  }
  process.exit(0);
});

// Serve index.html for client-side routing (SPA fallback)
if (existsSync(distPath)) {
  app.get('*', (req, res) => {
    res.sendFile(join(distPath, 'index.html'));
  });
}

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Redish Dashboard server running on http://0.0.0.0:${PORT}`);
});
