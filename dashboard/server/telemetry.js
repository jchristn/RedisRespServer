// Telemetry for the Redish dashboard backend (BFF).
//
// Signals: metrics (Prometheus scrape endpoint, default 127.0.0.1:9465/metrics) and traces (OTLP
// HTTP/protobuf, default http://127.0.0.1:4318). The BFF has no background work, so it ships no logs.
//
//   - One SERVER span per HTTP request, named "{METHOD} {route template}", adopting an inbound W3C
//     traceparent, plus http.server.request.duration (OpenTelemetry semantic-convention name).
//   - One CLIENT span per Redis command, named "redis {OPERATION}", plus integration metrics by
//     service, operation, and outcome. RESP has no header channel, so the trace cannot continue into
//     Redish itself; the Redish server starts its own trace per command.
//   - A gauge of pooled Redis connections, and Node.js runtime metrics.
//
// Every label is bounded: routes are templates, operations are drawn from a fixed command list (others
// become OTHER), and error types are fixed codes. Connection strings, keys, and values never appear on
// metrics or spans. All instrumentation is best-effort and never throws into request handling.
//
// Environment:
//   OTEL_SDK_DISABLED=true                 disable telemetry entirely
//   OTEL_SERVICE_NAME                       default "redish-dashboard"
//   OTEL_EXPORTER_OTLP_ENDPOINT             default "http://127.0.0.1:4318" (traces sent to /v1/traces)
//   OTEL_TRACES_EXPORTER=none               disable trace export (spans still feed nothing)
//   REDISH_DASHBOARD_METRICS_HOST           default "127.0.0.1" (use 0.0.0.0 in containers)
//   REDISH_DASHBOARD_METRICS_PORT           default 9465 (0 disables the scrape endpoint)

import { context, propagation, trace, SpanKind, SpanStatusCode, metrics as metricsApi } from '@opentelemetry/api';
import { W3CTraceContextPropagator } from '@opentelemetry/core';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { MeterProvider } from '@opentelemetry/sdk-metrics';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { NodeTracerProvider } from '@opentelemetry/sdk-trace-node';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';
import { PrometheusExporter } from '@opentelemetry/exporter-prometheus';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { RuntimeNodeInstrumentation } from '@opentelemetry/instrumentation-runtime-node';

export const SCOPE_NAME = 'redish-dashboard';

// All telemetry names in one place. Prometheus renders dots as underscores with unit and _total suffixes.
export const Names = Object.freeze({
  httpServerDuration: 'http.server.request.duration',
  redisCommands: 'redish.dashboard.redis.commands',
  redisCommandDuration: 'redish.dashboard.redis.command.duration',
  redisErrors: 'redish.dashboard.redis.errors',
  redisConnections: 'redish.dashboard.redis.connections',
  attrMethod: 'http.request.method',
  attrRoute: 'http.route',
  attrStatus: 'http.response.status_code',
  attrPeerService: 'peer.service',
  attrDbSystem: 'db.system.name',
  attrOperation: 'db.operation.name',
  attrOutcome: 'outcome',
  attrErrorType: 'error.type',
  peerServiceRedish: 'redish',
});

const SECONDS_BUCKETS = [0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10];

const KNOWN_OPERATIONS = new Set([
  'PING', 'ECHO', 'GET', 'SET', 'DEL', 'EXISTS', 'KEYS', 'FLUSHDB', 'AUTH', 'INFO', 'CLIENT', 'CONFIG',
  'SUBSCRIBE', 'UNSUBSCRIBE', 'PUBLISH', 'HELLO', 'COMMAND', 'SELECT', 'ROLE', 'TIME', 'MEMORY', 'SCAN',
  'TYPE', 'TTL', 'MGET', 'MSET', 'INCR', 'INCRBY', 'DECR', 'HMSET', 'HGETALL', 'INCRBYFLOAT', 'STRLEN',
  'GETRANGE', 'HSET', 'HGET', 'HDEL', 'HLEN', 'DBSIZE', 'EXPIRE', 'PERSIST', 'HSCAN', 'HEXISTS', 'RPUSH',
  'LPUSH', 'RPOP', 'LPOP', 'LRANGE', 'LLEN', 'SADD', 'SREM', 'SMEMBERS', 'SISMEMBER', 'SCARD', 'SPOP',
  'SRANDMEMBER', 'ZADD', 'ZREM', 'ZSCORE', 'ZCARD', 'ZRANGE', 'ZINCRBY', 'JSON.SET', 'JSON.GET', 'JSON.DEL',
  'XADD', 'XRANGE', 'XLEN', 'XDEL', 'XINFO', 'RENAME', 'RENAMENX', 'QUIT',
]);

const KNOWN_REPLY_CODES = new Set(['ERR', 'WRONGTYPE', 'WRONGPASS', 'NOAUTH', 'NOPERM', 'EXECABORT', 'BUSY', 'READONLY']);
const KNOWN_ERROR_CODES = new Set(['ECONNREFUSED', 'ECONNRESET', 'ETIMEDOUT', 'EHOSTUNREACH', 'ENOTFOUND', 'EPIPE']);

export function normalizeOperation(name) {
  const upper = String(name || '').toUpperCase();
  return KNOWN_OPERATIONS.has(upper) ? upper : 'OTHER';
}

export function classifyError(err) {
  if (!err) return 'unknown';
  if (err.name === 'ReplyError' && typeof err.message === 'string') {
    const code = err.message.split(' ')[0];
    return KNOWN_REPLY_CODES.has(code) ? code : 'OTHER';
  }
  if (err.code && KNOWN_ERROR_CODES.has(err.code)) return err.code;
  if (typeof err.message === 'string' && err.message.includes('Connection is closed')) return 'connection_closed';
  if (typeof err.message === 'string' && err.message.includes('Stream isn\'t writeable')) return 'not_connected';
  return err.name || 'Error';
}

function seconds(start) {
  return Number(process.hrtime.bigint() - start) / 1e9;
}

// Create the telemetry pipeline. Options exist so tests can inject in-memory readers and processors.
//   options.disabled            boolean
//   options.serviceName         string
//   options.metricReaders       MetricReader[] (replaces the Prometheus reader)
//   options.spanProcessors      SpanProcessor[] (replaces the OTLP processor)
//   options.runtimeMetrics      boolean (default true)
//   options.registerGlobal      boolean (default true): install the context manager and propagator
export function createTelemetry(options = {}) {
  const env = process.env;
  const disabled = options.disabled ?? (String(env.OTEL_SDK_DISABLED).toLowerCase() === 'true');

  const tracerProviderRef = { provider: null };
  const meterProviderRef = { provider: null };
  let tracer = trace.getTracer(SCOPE_NAME);
  let meter = metricsApi.getMeter(SCOPE_NAME);
  let instrumentationHandle = null;
  let scrapeUrl = null;

  if (!disabled) {
    try {
      const resource = resourceFromAttributes({
        'service.name': options.serviceName || env.OTEL_SERVICE_NAME || 'redish-dashboard',
        'service.version': env.npm_package_version || '1.0.0',
      });

      let spanProcessors = options.spanProcessors;
      if (!spanProcessors) {
        spanProcessors = [];
        if (String(env.OTEL_TRACES_EXPORTER).toLowerCase() !== 'none') {
          const base = (env.OTEL_EXPORTER_OTLP_ENDPOINT || 'http://127.0.0.1:4318').replace(/\/$/, '');
          spanProcessors.push(new BatchSpanProcessor(new OTLPTraceExporter({ url: base + '/v1/traces' })));
        }
      }

      let metricReaders = options.metricReaders;
      if (!metricReaders) {
        metricReaders = [];
        const port = parseInt(env.REDISH_DASHBOARD_METRICS_PORT ?? '9465', 10);
        if (port > 0) {
          const host = env.REDISH_DASHBOARD_METRICS_HOST || '127.0.0.1';
          metricReaders.push(new PrometheusExporter({ host, port }));
          scrapeUrl = `http://${host}:${port}/metrics`;
        }
      }

      const tracerProvider = new NodeTracerProvider({ resource, spanProcessors });
      if (options.registerGlobal !== false) {
        tracerProvider.register({ propagator: new W3CTraceContextPropagator() });
      }
      tracerProviderRef.provider = tracerProvider;
      tracer = tracerProvider.getTracer(SCOPE_NAME);

      const meterProvider = new MeterProvider({ resource, readers: metricReaders });
      meterProviderRef.provider = meterProvider;
      meter = meterProvider.getMeter(SCOPE_NAME);

      if (options.runtimeMetrics !== false) {
        instrumentationHandle = registerInstrumentations({
          instrumentations: [new RuntimeNodeInstrumentation()],
          meterProvider,
          tracerProvider,
        });
      }
    } catch (err) {
      // Best-effort: run without telemetry rather than fail to start.
      console.warn('[telemetry] failed to start, continuing without telemetry:', err?.message || err);
    }
  }

  const httpDuration = meter.createHistogram(Names.httpServerDuration, {
    unit: 's',
    description: 'Duration of HTTP server requests handled by the dashboard backend.',
    advice: { explicitBucketBoundaries: SECONDS_BUCKETS },
  });
  const redisCommands = meter.createCounter(Names.redisCommands, {
    unit: '{command}',
    description: 'Redis commands sent to Redish, by operation and outcome.',
  });
  const redisDuration = meter.createHistogram(Names.redisCommandDuration, {
    unit: 's',
    description: 'Latency of Redis commands sent to Redish, by operation and outcome.',
    advice: { explicitBucketBoundaries: SECONDS_BUCKETS },
  });
  const redisErrors = meter.createCounter(Names.redisErrors, {
    unit: '{error}',
    description: 'Failed Redis commands, by operation and error type.',
  });
  const redisConnections = meter.createObservableGauge(Names.redisConnections, {
    unit: '{connection}',
    description: 'Redis connections held in the backend connection pool.',
  });

  let poolSizeProvider = () => 0;
  redisConnections.addCallback((result) => {
    try {
      result.observe(poolSizeProvider(), { [Names.attrPeerService]: Names.peerServiceRedish });
    } catch {
      // ignore
    }
  });

  // Express middleware: a SERVER span per request and the request-duration histogram, labeled by route
  // template (known only after routing, so the span is renamed when the response finishes).
  function httpMiddleware(req, res, next) {
    let span = null;
    let ctx = context.active();
    const start = process.hrtime.bigint();
    try {
      ctx = propagation.extract(context.active(), req.headers);
      span = tracer.startSpan(`${req.method}`, {
        kind: SpanKind.SERVER,
        attributes: {
          [Names.attrMethod]: req.method,
          'url.path': req.path,
          'url.scheme': req.protocol,
          'user_agent.original': req.get('user-agent') || undefined,
          'client.address': req.ip,
        },
      }, ctx);
      ctx = trace.setSpan(ctx, span);
    } catch {
      span = null;
    }

    res.on('finish', () => {
      try {
        const route = req.route?.path ? `${req.baseUrl || ''}${req.route.path}` : (req.path?.startsWith('/api/') ? 'unmatched' : 'static');
        const status = res.statusCode;
        httpDuration.record(seconds(start), {
          [Names.attrMethod]: req.method,
          [Names.attrRoute]: route,
          [Names.attrStatus]: status,
        });
        if (span) {
          span.updateName(`${req.method} ${route}`);
          span.setAttribute(Names.attrRoute, route);
          span.setAttribute(Names.attrStatus, status);
          if (status >= 500) span.setStatus({ code: SpanStatusCode.ERROR, message: `HTTP ${status}` });
          span.end();
        }
      } catch {
        // ignore
      }
    });

    context.with(ctx, next);
  }

  // Wrap an ioredis client so every command gets a CLIENT span and integration metrics.
  function instrumentRedisClient(client) {
    if (!client || client.__redishTelemetry) return client;
    try {
      const original = client.sendCommand.bind(client);
      client.sendCommand = (command, stream) => {
        try {
          const operation = normalizeOperation(command?.name);
          const start = process.hrtime.bigint();
          const span = tracer.startSpan(`redis ${operation}`, {
            kind: SpanKind.CLIENT,
            attributes: {
              [Names.attrDbSystem]: 'redis',
              [Names.attrOperation]: operation,
              [Names.attrPeerService]: Names.peerServiceRedish,
              'server.address': client.options?.host,
              'server.port': client.options?.port,
            },
          });

          const finish = (err) => {
            try {
              const outcome = err ? 'error' : 'ok';
              const attrs = {
                [Names.attrPeerService]: Names.peerServiceRedish,
                [Names.attrOperation]: operation,
                [Names.attrOutcome]: outcome,
              };
              redisCommands.add(1, attrs);
              redisDuration.record(seconds(start), attrs);
              if (err) {
                const errorType = classifyError(err);
                redisErrors.add(1, {
                  [Names.attrPeerService]: Names.peerServiceRedish,
                  [Names.attrOperation]: operation,
                  [Names.attrErrorType]: errorType,
                });
                span.setAttribute(Names.attrErrorType, errorType);
                span.recordException(err);
                span.setStatus({ code: SpanStatusCode.ERROR, message: errorType });
              } else {
                span.setStatus({ code: SpanStatusCode.OK });
              }
              span.end();
            } catch {
              // ignore
            }
          };

          if (command?.promise?.then) command.promise.then(() => finish(null), (err) => finish(err));
          else finish(null);
        } catch {
          // ignore: never block the command
        }
        return original(command, stream);
      };
      Object.defineProperty(client, '__redishTelemetry', { value: true });
    } catch {
      // ignore
    }
    return client;
  }

  function setPoolSizeProvider(fn) {
    if (typeof fn === 'function') poolSizeProvider = fn;
  }

  async function shutdown() {
    try { instrumentationHandle?.(); } catch { /* ignore */ }
    try { await tracerProviderRef.provider?.shutdown(); } catch { /* ignore */ }
    try { await meterProviderRef.provider?.shutdown(); } catch { /* ignore */ }
  }

  async function forceFlush() {
    try { await tracerProviderRef.provider?.forceFlush(); } catch { /* ignore */ }
    try { await meterProviderRef.provider?.forceFlush(); } catch { /* ignore */ }
  }

  return {
    enabled: !disabled && tracerProviderRef.provider !== null,
    scrapeUrl,
    tracer,
    meter,
    httpMiddleware,
    instrumentRedisClient,
    setPoolSizeProvider,
    forceFlush,
    shutdown,
  };
}
