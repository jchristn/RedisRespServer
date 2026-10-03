// Proves the dashboard backend emits its documented telemetry, using in-memory readers and exporters.
// Run with: npm test
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import express from 'express';
import Redis from 'ioredis';
import { trace, SpanKind, SpanStatusCode } from '@opentelemetry/api';
import { MetricReader } from '@opentelemetry/sdk-metrics';
import { InMemorySpanExporter, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { createTelemetry, normalizeOperation, classifyError, Names } from './telemetry.js';

class TestMetricReader extends MetricReader {
  async onShutdown() {}
  async onForceFlush() {}
}

const spans = new InMemorySpanExporter();
const reader = new TestMetricReader();
let telemetry;
let fakeRedis;
let fakeRedisPort;

function points(resourceMetrics, name) {
  const out = [];
  for (const scope of resourceMetrics.scopeMetrics) {
    for (const metric of scope.metrics) {
      if (metric.descriptor.name === name) out.push(...metric.dataPoints);
    }
  }
  return out;
}

async function collect() {
  const { resourceMetrics } = await reader.collect();
  return resourceMetrics;
}

function sumOf(dataPoints, attrs) {
  return dataPoints
    .filter((p) => Object.entries(attrs).every(([k, v]) => p.attributes[k] === v))
    .reduce((acc, p) => acc + (typeof p.value === 'number' ? p.value : p.value.count), 0);
}

before(async () => {
  telemetry = createTelemetry({
    serviceName: 'redish-dashboard-test',
    metricReaders: [reader],
    spanProcessors: [new SimpleSpanProcessor(spans)],
    runtimeMetrics: false,
  });

  // Minimal RESP server: replies an error to BADCMD and +PONG to everything else.
  fakeRedis = net.createServer((socket) => {
    socket.on('data', (buf) => {
      const text = buf.toString('latin1');
      const frames = text.split('*').filter((f) => f.length > 0);
      for (const frame of frames) {
        socket.write(frame.toUpperCase().includes('BADCMD') ? '-ERR unknown command\r\n' : '+PONG\r\n');
      }
    });
  });
  await new Promise((resolve) => fakeRedis.listen(0, '127.0.0.1', resolve));
  fakeRedisPort = fakeRedis.address().port;
});

after(async () => {
  await telemetry.shutdown();
  await new Promise((resolve) => fakeRedis.close(resolve));
});

test('operation names are bounded to a fixed command list', () => {
  assert.equal(normalizeOperation('get'), 'GET');
  assert.equal(normalizeOperation('json.get'), 'JSON.GET');
  assert.equal(normalizeOperation('user-typed-anything'), 'OTHER');
  assert.equal(normalizeOperation(undefined), 'OTHER');
});

test('errors are classified into fixed codes', () => {
  const reply = new Error('WRONGTYPE Operation against a key');
  reply.name = 'ReplyError';
  assert.equal(classifyError(reply), 'WRONGTYPE');
  const other = new Error('SOMETHING custom');
  other.name = 'ReplyError';
  assert.equal(classifyError(other), 'OTHER');
  const refused = new Error('connect ECONNREFUSED');
  refused.code = 'ECONNREFUSED';
  assert.equal(classifyError(refused), 'ECONNREFUSED');
  assert.equal(classifyError(new Error('Connection is closed.')), 'connection_closed');
});

test('HTTP requests produce a server span and duration labeled by route template', async () => {
  const app = express();
  app.use(telemetry.httpMiddleware);
  app.get('/api/items/:id', (req, res) => res.json({ ok: true }));
  app.get('/api/fail', (req, res) => res.status(500).json({ error: 'boom' }));
  const server = await new Promise((resolve) => {
    const s = app.listen(0, '127.0.0.1', () => resolve(s));
  });
  const base = `http://127.0.0.1:${server.address().port}`;

  const inboundTrace = '0af7651916cd43dd8448eb211c80319c';
  await fetch(`${base}/api/items/42`, { headers: { traceparent: `00-${inboundTrace}-b7ad6b7169203331-01` } });
  await fetch(`${base}/api/fail`);
  await new Promise((resolve) => server.close(resolve));

  const finished = spans.getFinishedSpans();
  const ok = finished.find((s) => s.name === 'GET /api/items/:id');
  assert.ok(ok, 'span named by route template');
  assert.equal(ok.kind, SpanKind.SERVER);
  assert.equal(ok.spanContext().traceId, inboundTrace, 'inbound traceparent adopted');
  assert.equal(ok.attributes[Names.attrStatus], 200);

  const failed = finished.find((s) => s.name === 'GET /api/fail');
  assert.ok(failed);
  assert.equal(failed.status.code, SpanStatusCode.ERROR);

  const rm = await collect();
  const dps = points(rm, Names.httpServerDuration);
  assert.ok(sumOf(dps, { [Names.attrRoute]: '/api/items/:id', [Names.attrStatus]: 200 }) >= 1, 'duration by route template');
  assert.ok(sumOf(dps, { [Names.attrRoute]: '/api/fail', [Names.attrStatus]: 500 }) >= 1, 'failure recorded');
  assert.ok(!dps.some((p) => String(p.attributes[Names.attrRoute]).includes('42')), 'no concrete ids in route label');
});

test('Redis commands produce client spans and integration metrics, including failures', async () => {
  const client = telemetry.instrumentRedisClient(new Redis({
    host: '127.0.0.1',
    port: fakeRedisPort,
    enableReadyCheck: false,
    lazyConnect: true,
    retryStrategy: () => null,
  }));
  await client.connect();

  const tracer = trace.getTracer('test');
  await tracer.startActiveSpan('parent', async (parent) => {
    assert.equal(await client.ping(), 'PONG');
    await assert.rejects(client.call('BADCMD', 'secret-key'));
    parent.end();
  });
  client.disconnect();

  const finished = spans.getFinishedSpans();
  const ping = finished.find((s) => s.name === 'redis PING');
  assert.ok(ping, 'PING client span');
  assert.equal(ping.kind, SpanKind.CLIENT);
  assert.equal(ping.attributes[Names.attrPeerService], 'redish');
  const parent = finished.find((s) => s.name === 'parent');
  assert.equal(ping.parentSpanContext?.spanId ?? ping.parentSpanId, parent.spanContext().spanId, 'client span nests under the active span');

  const bad = finished.find((s) => s.name === 'redis OTHER');
  assert.ok(bad, 'unknown command bounded to OTHER');
  assert.equal(bad.status.code, SpanStatusCode.ERROR);
  assert.ok(!JSON.stringify(bad.attributes).includes('secret-key'), 'arguments never recorded');

  const rm = await collect();
  assert.ok(sumOf(points(rm, Names.redisCommands), { [Names.attrOperation]: 'PING', [Names.attrOutcome]: 'ok' }) >= 1);
  assert.ok(sumOf(points(rm, Names.redisCommandDuration), { [Names.attrOperation]: 'PING' }) >= 1);
  assert.ok(sumOf(points(rm, Names.redisErrors), { [Names.attrOperation]: 'OTHER', [Names.attrErrorType]: 'ERR' }) >= 1);
});

test('a closed connection is recorded as a failed integration call', async () => {
  const client = telemetry.instrumentRedisClient(new Redis({
    host: '127.0.0.1',
    port: 1,
    enableReadyCheck: false,
    lazyConnect: true,
    enableOfflineQueue: false,
    retryStrategy: () => null,
  }));
  await assert.rejects(client.ping());
  client.disconnect();

  const rm = await collect();
  assert.ok(sumOf(points(rm, Names.redisCommands), { [Names.attrOperation]: 'PING', [Names.attrOutcome]: 'error' }) >= 1);
});

test('connection pool gauge reports the provided pool size', async () => {
  telemetry.setPoolSizeProvider(() => 3);
  const rm = await collect();
  const dps = points(rm, Names.redisConnections);
  assert.ok(dps.some((p) => p.value === 3));
});

test('disabled telemetry never throws on the request or Redis path', async () => {
  const off = createTelemetry({ disabled: true, runtimeMetrics: false, registerGlobal: false });
  assert.equal(off.enabled, false);
  const app = express();
  app.use(off.httpMiddleware);
  app.get('/x', (req, res) => res.send('ok'));
  const server = await new Promise((resolve) => {
    const s = app.listen(0, '127.0.0.1', () => resolve(s));
  });
  const res = await fetch(`http://127.0.0.1:${server.address().port}/x`);
  assert.equal(await res.text(), 'ok');
  await new Promise((resolve) => server.close(resolve));

  const client = off.instrumentRedisClient(new Redis({ host: '127.0.0.1', port: fakeRedisPort, enableReadyCheck: false, lazyConnect: true }));
  await client.connect();
  assert.equal(await client.ping(), 'PONG');
  client.disconnect();
  await off.shutdown();
});
