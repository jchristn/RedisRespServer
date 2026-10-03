# Telemetry

This repository ships metrics, traces, and logs for every component, plus a local observability stack
(Prometheus, Tempo, Loki, Grafana) that comes up with one command. An on-call engineer can answer
*where the time went* and *what failed* from Grafana alone.

| Component | Kind | Emits through | Exported by |
|---|---|---|---|
| `RedisRespServer` (NuGet library) | Library | BCL `Meter` + `ActivitySource` named `RedisRespServer` | Whatever host subscribes (no SDK dependency) |
| `Redish.Server` | Service | BCL `Meter` + `ActivitySource` named `Redish.Server`, server log lines | One [Radiant](https://www.nuget.org/packages/Radiant) host (OTLP, Prometheus, Loki, .NET runtime metrics) |
| Dashboard backend (`dashboard/server`) | Service | OpenTelemetry JS API + SDK | OTLP/HTTP traces, Prometheus scrape endpoint |

Contents:

1. [Quick start](#quick-start)
2. [Sources and subscribing](#sources-and-subscribing)
3. [Configuration](#configuration)
4. [Metrics catalog](#metrics-catalog)
5. [Spans catalog](#spans-catalog)
6. [Logs](#logs)
7. [Trace topology and propagation](#trace-topology-and-propagation)
8. [Dashboards](#dashboards)
9. [Recommended alerts](#recommended-alerts)
10. [Cardinality, privacy, and safety rules](#cardinality-privacy-and-safety-rules)
11. [Production notes](#production-notes)
12. [Tests](#tests)

## Quick start

```bash
docker compose -f docker/compose.yaml up -d --build
```

| Service | URL | Credentials |
|---|---|---|
| Grafana | http://localhost:3000 | `admin` / `admin` (local default; see [Production notes](#production-notes)) |
| Prometheus | http://localhost:9090 | none |
| Tempo API | http://localhost:3200 (OTLP gRPC 4317, OTLP HTTP 4318) | none |
| Loki API | http://localhost:3100 | none |
| Redish (RESP) | `127.0.0.1:6379` | none |
| Redish metrics | `http://redish:9464/metrics` inside the compose network | none |
| Dashboard | http://localhost:3002 (connect to `redish:6379`) | none |
| Dashboard backend metrics | http://localhost:9465/metrics | none |

Open Grafana, then **Dashboards → Redish → Redish / Overview**. The dashboard's Overview page also links to
every service above from its **External Services** card.

The Redish scrape endpoint answers only to the host name it binds (`redish`, which is what Prometheus scrapes).
It is published on host port 9464, but a browser request to `localhost:9464` does not match that name. Query
the data through Prometheus or Grafana instead.

## Sources and subscribing

| Name | Type | Owner | Notes |
|---|---|---|---|
| `RedisRespServer` | Meter and ActivitySource | `RedisRespServer` library | Constants in `RedisResp.RespTelemetry` |
| `Redish.Server` | Meter and ActivitySource | Redish server | Constants in `Redish.Server.Telemetry.RedishTelemetry` |
| `redish-server` | Meter | Radiant host (process metrics) | `process.memory.usage`, `process.thread.count`, `process.uptime` |
| `OpenTelemetry.Instrumentation.Runtime` | Meter | Radiant host | .NET GC, JIT, thread pool, locks, exceptions |
| `redish-dashboard` | Meter and Tracer | Dashboard backend | Names in `dashboard/server/telemetry.js` (`Names`) |

These names are a public contract: dashboards and alerts depend on them, and they do not change between releases.

### Subscribing to the library from your own host

`RedisRespServer` emits through `System.Diagnostics` only. Until something subscribes, each measurement is an
enabled check and a return, and `StartActivity` returns null. To collect it:

```csharp
// Radiant
RadiantSettings settings = new RadiantSettings("my-redis-service");
settings.Sources.AddMeter(RespTelemetry.MeterName);              // "RedisRespServer"
settings.Sources.AddActivitySource(RespTelemetry.ActivitySourceName);
settings.Prometheus.Enable = true;
using (RadiantHost host = RadiantHost.Start(settings)) { /* run your RespListener */ }

// Plain OpenTelemetry SDK
using MeterProvider meters = Sdk.CreateMeterProviderBuilder().AddMeter("RedisRespServer").AddOtlpExporter().Build();
using TracerProvider traces = Sdk.CreateTracerProviderBuilder().AddSource("RedisRespServer").AddOtlpExporter().Build();
```

You can also watch it live with `dotnet-counters monitor --counters RedisRespServer -n <process>`.

Histogram bucket boundaries travel with the instruments as `InstrumentAdvice`, so OpenTelemetry SDK 1.10 or
later uses sensible second and byte buckets without any view configuration.

## Configuration

### Redish server (`redish.json`, `Telemetry` section)

Changes take effect on restart. Telemetry is best-effort: if the pipeline cannot start (for example the
Prometheus port is taken), the server logs a warning with the underlying cause and runs without it.

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. When false nothing is exported and instrumentation stays a no-op. |
| `ServiceName` | `redish-server` | `service.name` resource attribute. |
| `EnableMetrics` | `true` | Collect and export metrics (including the Prometheus endpoint). |
| `EnableTraces` | `true` | Collect and export traces. |
| `EnableLogs` | `true` | Forward server log lines into the OpenTelemetry logs pipeline. Console, file, and syslog logging are unaffected. |
| `TraceSamplingRatio` | `1.0` | Head sampling ratio, 0.0 to 1.0 (clamped). Parent-based. |
| `LogMinimumSeverity` | `2` | Radiant scale: 0 Trace, 1 Debug, 2 Information, 4 Warning, 5 Error, 6 Critical, 7 none. Debug lines can contain key names and values, so keep this at 2 or higher. |
| `OtlpEnabled` | `true` | Push metrics, traces, and logs over OTLP. |
| `OtlpEndpoint` | `http://127.0.0.1:4317` | Collector or Tempo endpoint. |
| `OtlpProtocol` | `grpc` | `grpc` (port 4317) or `http` (HTTP/protobuf, port 4318). |
| `PrometheusEnabled` | `true` | Serve `/metrics` in-process. Anonymous; keep it on an internal network. |
| `PrometheusHostname` | `127.0.0.1` | Host name the endpoint binds and answers to. In containers use the name Prometheus scrapes (the compose stack uses `redish`). Wildcards are rejected by the OpenTelemetry listener. |
| `PrometheusPort` | `9464` | Scrape port (1 to 65535). |
| `LokiEnabled` | `false` | Push logs straight to Loki's OTLP endpoint. |
| `LokiEndpoint` | `http://127.0.0.1:3100/otlp` | Loki OTLP base URL. |

The compose stack uses `docker/redish.compose.json` (OTLP to `tempo:4317`, Loki enabled, scrape host `redish`).

### Dashboard backend (environment variables)

| Variable | Default | Meaning |
|---|---|---|
| `OTEL_SDK_DISABLED` | unset | `true` disables telemetry. |
| `OTEL_SERVICE_NAME` | `redish-dashboard` | `service.name`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://127.0.0.1:4318` | OTLP/HTTP base URL; traces go to `/v1/traces`. |
| `OTEL_TRACES_EXPORTER` | unset | `none` disables trace export. |
| `REDISH_DASHBOARD_METRICS_HOST` | `127.0.0.1` | Scrape endpoint bind address (`0.0.0.0` in containers). |
| `REDISH_DASHBOARD_METRICS_PORT` | `9465` | Scrape endpoint port; `0` disables it. |
| `GRAFANA_URL`, `PROMETHEUS_URL`, `TEMPO_URL`, `LOKI_URL`, `REDISH_METRICS_URL`, `DASHBOARD_METRICS_URL` | `http://localhost:<port>` | Links on the External Services card. Set one to an empty string to hide that service. |
| `GRAFANA_USERNAME`, `GRAFANA_PASSWORD` | `admin` / `admin` | Credentials shown on the card. |

The dashboard backend has no background work, so it ships no logs.

## Metrics catalog

Prometheus names are what you query. The .NET exporter appends unit and `_total` suffixes. The JavaScript
exporter appends `_total` to counters but no unit suffix. Histograms add `_bucket`, `_sum`, and `_count`.
Derive quantiles with `histogram_quantile` in Grafana; nothing is precomputed in-process.

### `RedisRespServer` library

| Instrument | Prometheus series | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `redisresp.listeners.active` | `redisresp_listeners_active` | UpDownCounter | `{listener}` | none | Listeners currently accepting connections. |
| `redisresp.connections.active` | `redisresp_connections_active` | UpDownCounter | `{connection}` | none | Client connections currently open. |
| `redisresp.connections.accepted` | `redisresp_connections_accepted_total` | Counter | `{connection}` | none | Connections accepted. |
| `redisresp.connections.closed` | `redisresp_connections_closed_total` | Counter | `{connection}` | `close_reason` | Connections closed: `client_closed`, `client_reset`, `server_disconnect`, `server_shutdown`, `error`. |
| `redisresp.connection.duration` | `redisresp_connection_duration_seconds` | Histogram | `s` | `close_reason` | Connection lifetime. |
| `redisresp.accept.errors` | `redisresp_accept_errors_total` | Counter | `{error}` | `error_type` | Failures accepting a connection. |
| `redisresp.network.received` | `redisresp_network_received_bytes_total` | Counter | `By` | none | Bytes read from client sockets. |
| `redisresp.messages.received` | `redisresp_messages_received_total` | Counter | `{message}` | `resp_type`, `resp_protocol` | Complete RESP messages parsed. |
| `redisresp.message.size` | `redisresp_message_size_bytes` | Histogram | `By` | `resp_type` | Size of each complete message. |
| `redisresp.dispatch.duration` | `redisresp_dispatch_duration_seconds` | Histogram | `s` | `resp_type`, `outcome` | Time running subscribers for one message. `outcome`: `ok`, `error` (a subscriber threw), `unhandled` (parsed but matched no dispatch path). |
| `redisresp.parse.errors` | `redisresp_parse_errors_total` | Counter | `{error}` | `error_type` | Input the parser cannot interpret (`unknown_prefix`), once per connection. |
| `redisresp.client.errors` | `redisresp_client_errors_total` | Counter | `{error}` | `error_type` | Unexpected errors that ended a connection loop. |

`resp_type` is a `RespDataType` name (`Array`, `BulkString`, `SimpleString`, and so on) or `Unknown`.
`resp_protocol` is `RESP2`, `RESP3`, or `Unknown`.

### `Redish.Server`

| Instrument | Prometheus series | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `redish.commands` | `redish_commands_total` | Counter | `{command}` | `db_operation_name`, `outcome` | Commands processed. `outcome`: `ok`, `error` (error reply), `exception` (handler threw), `unknown_command`. |
| `redish.command.duration` | `redish_command_duration_seconds` | Histogram | `s` | `db_operation_name`, `outcome` | End-to-end command latency (execute plus respond). |
| `redish.command.stage.duration` | `redish_command_stage_duration_seconds` | Histogram | `s` | `redish_stage`, `db_operation_name` | Per-stage latency: `execute` (against storage), `respond` (socket write). |
| `redish.command.errors` | `redish_command_errors_total` | Counter | `{error}` | `db_operation_name`, `error_type` | Failed commands. `error_type`: reply code (`ERR`, `WRONGTYPE`, `WRONGPASS`, `NOAUTH`, `NOPERM`, `OTHER`, ...) or exception type name. |
| `redish.response.size` | `redish_response_size_bytes` | Histogram | `By` | `db_operation_name` | Reply size. |
| `redish.response.errors` | `redish_response_errors_total` | Counter | `{error}` | `error_type` | Replies not delivered: `client_gone` or a socket exception type. |
| `redish.keyspace.lookups` | `redish_keyspace_lookups_total` | Counter | `{lookup}` | `db_operation_name`, `result` | `GET`, `HGET`, `JSON.GET`, `ZSCORE` lookups; `result` is `hit` or `miss`. |
| `redish.auth.attempts` | `redish_auth_attempts_total` | Counter | `{attempt}` | `outcome` | `success`, `failure`, `not_required`, `invalid_arguments`. |
| `redish.clients` | `redish_clients` | Gauge | `{client}` | `resp_protocol` | Tracked clients by negotiated protocol. |
| `redish.storage.keys` | `redish_storage_keys` | Gauge | `{key}` | none | Keys in storage, including expired keys not yet swept. |
| `redish.expiration.sweeps` | `redish_expiration_sweeps_total` | Counter | `{sweep}` | `outcome` | TTL sweep jobs (every second): `success`, `failure`. |
| `redish.expiration.sweep.duration` | `redish_expiration_sweep_duration_seconds` | Histogram | `s` | `outcome` | Sweep duration from timer fire to completion. |
| `redish.expiration.stage.duration` | `redish_expiration_stage_duration_seconds` | Histogram | `s` | `redish_stage`, `outcome` | `queued` (waiting for a pool thread), `snapshot` (copy key list), `evict` (check and remove). |
| `redish.expiration.stage.events` | `redish_expiration_stage_events_total` | Counter | `{stage}` | `redish_stage`, `outcome` | Stage executions. |
| `redish.expiration.sweeps.active` | `redish_expiration_sweeps_active` | UpDownCounter | `{sweep}` | none | Sweeps running now. Above 1 means sweeps overlap. |
| `redish.expiration.keys.scanned` | `redish_expiration_keys_scanned_total` | Counter | `{key}` | none | Keys examined by sweeps. |
| `redish.expiration.keys.expired` | `redish_expiration_keys_expired_total` | Counter | `{key}` | `trigger` | Keys removed after their TTL: `active` (sweep) or `passive` (on access). |
| `redish.expiration.last_success` | `redish_expiration_last_success_seconds` | Gauge | `s` | none | Unix time of the last successful sweep (0 before the first). |
| `redish.build.info` | `redish_build_info` | Gauge | `{info}` | `service_version`, `redish_storage_mode`, `redish_redis_version` | Always 1; facts carried as labels. |
| `redish.config.databases` | `redish_config_databases` | Gauge | `{database}` | none | Configured logical database count. |
| `redish.uptime` | `redish_uptime_seconds` | Gauge | `s` | none | Seconds since the server started. |

`db_operation_name` is the upper-case command name for the 75 supported commands, or `UNKNOWN`.

### Dashboard backend

| Instrument | Prometheus series | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `http.server.request.duration` | `http_server_request_duration` | Histogram | `s` | `http_request_method`, `http_route`, `http_response_status_code` | Per request. `http_route` is the Express route template (`/api/keys/:key`), `static` for the SPA, or `unmatched`. |
| `redish.dashboard.redis.commands` | `redish_dashboard_redis_commands_total` | Counter | `{command}` | `peer_service`, `db_operation_name`, `outcome` | Integration calls to Redish. `outcome`: `ok` or `error`. |
| `redish.dashboard.redis.command.duration` | `redish_dashboard_redis_command_duration` | Histogram | `s` | `peer_service`, `db_operation_name`, `outcome` | Round-trip latency to Redish. |
| `redish.dashboard.redis.errors` | `redish_dashboard_redis_errors_total` | Counter | `{error}` | `peer_service`, `db_operation_name`, `error_type` | Failed calls: reply code, socket code (`ECONNREFUSED`, ...), `connection_closed`, `not_connected`. |
| `redish.dashboard.redis.connections` | `redish_dashboard_redis_connections` | Gauge | `{connection}` | `peer_service` | Connections held in the backend's pool. |

`db_operation_name` here is drawn from a fixed command list; anything typed into the console that is not on it becomes `OTHER`.

### Runtime (third-party instrumentation)

- **Redish (.NET, via Radiant):** `process_memory_usage_bytes`, `process_thread_count`, `process_uptime_seconds`,
  and `process_runtime_dotnet_*` (GC collections, heap size, allocations, pause time, JIT, thread pool, lock contention, exceptions).
- **Dashboard backend (Node.js):** `nodejs_eventloop_utilization`, `nodejs_eventloop_delay_*`, `v8js_memory_heap_*`, `v8js_gc_duration`.
  The `nodejs_eventloop_delay_p50/p90/p99` series are precomputed by that instrumentation; the dashboards use
  utilization and mean or max instead.

## Spans catalog

| Span | Kind | Source | Parent | Attributes | Status |
|---|---|---|---|---|---|
| `resp.dispatch {RespDataType}` | Server | `RedisRespServer` | Root (one trace per message) | `resp.type`, `resp.protocol`, `resp.message.size`, `outcome`, `resp.client.id`, `client.address`, `client.port`, `error.type` | `Ok`; `Error` when a subscriber throws (exception event attached) or the message is unhandled |
| `redish.command {OPERATION}` | Internal | `Redish.Server` | `resp.dispatch Array` | `db.system.name=redis`, `db.operation.name`, `redish.args.count`, `resp.client.id`, `outcome`, `redish.response.size`, `error.type` | `Ok`; `Error` on an error reply or exception |
| `stage:execute` | Internal | `Redish.Server` | `redish.command` | none | `Error` with exception event if the command threw |
| `stage:respond` | Internal | `Redish.Server` | `redish.command` | none | `Ok` |
| `redish.expiration.sweep` | Internal | `Redish.Server` | Root | `redish.keys.scanned`, `redish.keys.expired`, `error.type` | `Ok`; `Error` with exception event on failure |
| `stage:queued`, `stage:snapshot`, `stage:evict` | Internal | `Redish.Server` | `redish.expiration.sweep` | none | `Error` on the stage that failed |
| `{METHOD} {route}` | Server | `redish-dashboard` | Inbound `traceparent`, else root | `http.request.method`, `http.route`, `http.response.status_code`, `url.path`, `url.scheme`, `client.address`, `user_agent.original` | `Error` on 5xx |
| `redis {OPERATION}` | Client | `redish-dashboard` | The HTTP server span | `db.system.name`, `db.operation.name`, `peer.service=redish`, `server.address`, `server.port`, `error.type` | `Error` with exception on failure |

Sweeps run every second. Only sweeps that expired keys or failed are traced; idle sweeps emit metrics only, so
Tempo is not flooded. Sweep spans are built after the fact from recorded timestamps.

Useful TraceQL in Grafana Explore (Tempo):

- `{ status = error }`: every failed command, sweep, or 5xx.
- `{ name =~ "redish.command.*" && duration > 10ms }`: slow commands.
- `{ span.db.operation.name = "GET" }`: one command type.
- `{ name = "redish.expiration.sweep" }`: sweeps that did work.
- `{ resource.service.name = "redish-dashboard" && kind = client && status = error }`: failing calls from the dashboard to Redish.

## Logs

Redish forwards its log lines (the same lines it writes to console, file, and syslog) into the OpenTelemetry
logs pipeline at `LogMinimumSeverity` and above (Information by default), and to Loki when `LokiEnabled` is
true. Lines written while a span is active carry `trace_id` and `span_id`, so Grafana links a log line to its
trace (the Loki datasource defines a `TraceID` derived field, and the Tempo datasource links traces to logs).
Debug lines, which can contain key names and values, are not exported at the default severity.

Query in Grafana: `{service_name="redish-server"}`, or add `| severity_text=~"(?i)warn.*|error"` for problems.

## Trace topology and propagation

- **Inbound RESP to Redish:** RESP has no header channel, so no trace context can arrive with a command. Each
  message starts a new trace at `resp.dispatch`. The library clears any ambient span on its accept loop, so
  traces never inherit an unrelated caller span.
- **Inside Redish:** the command handler runs on an `async void` event subscriber. `Activity.Current` flows
  through the async continuation, so `redish.command` and its stages nest under `resp.dispatch` in one trace.
  The dispatch span can end before the command span finishes, because event dispatch returns at the handler's
  first await.
- **Background hand-off:** the expiration sweep is an explicit root, built independently of whatever context
  created the storage timer.
- **Browser to dashboard backend:** W3C `traceparent` is adopted when present.
- **Dashboard backend to Redish:** client spans record the call, but the trace cannot continue into Redish (no
  headers in RESP). Correlate by time and by `db.operation.name` instead.

## Dashboards

Grafana is provisioned as code: datasources in `docker/grafana/provisioning/datasources` (stable UIDs
`prometheus`, `tempo`, `loki`) and dashboard JSON in `assets/grafana/`, loaded into the **Redish** folder.

| Dashboard | File | Answers |
|---|---|---|
| Redish / Overview | `redish-overview.json` | Is it up? Traffic, error ratio, p95, connections, keys, sweep freshness, build facts, every failure kind, recent error traces, and warning logs. Start here. |
| Redish / Commands | `redish-commands.json` | Which commands are slow or failing, and whether time goes to `execute` or `respond`. Keyspace hit ratio, reply sizes, undelivered replies, AUTH, clients by protocol. |
| Redish / Connections and Protocol | `redish-connections.json` | Library layer: open connections, close reasons, lifetimes, bytes, messages by type and protocol, dispatch latency, parse, accept, and dispatch failures. |
| Redish / Expiration and Storage | `redish-expiration.json` | The background TTL job: freshness, failures, overlap, duration, per-stage p95 (including queue wait), keys expired by trigger, keys scanned, storage size, and logs. |
| Redish / Runtime | `redish-runtime.json` | .NET process: memory, threads, thread-pool queue, GC, allocation rate, lock contention. |
| Redish / Dashboard Backend | `redish-dashboard-backend.json` | The BFF: requests by route and status, 5xx ratio, p95 by route, and the Redish integration (calls, latency, errors, pool). |

Worked example. The Overview error ratio rises. **Failures by kind** shows `command WRONGTYPE`.
**Commands → Errors by command and type** names the command. Overview's **Recent error traces** opens one
failing `redish.command` span, where `error.type` and the parent `resp.dispatch` span give the client address.

Measured on a development machine: a `GET` takes about 1 to 2.5 ms end to end, and most of that time is in
`stage:respond`. `SendStringResponse` deliberately waits 1 ms after every reply, which is the floor visible in
**p95 by stage**.

## Recommended alerts

```yaml
groups:
  - name: redish
    rules:
      - alert: RedishDown
        expr: up{job="redish"} == 0
        for: 1m
        annotations: { summary: "Redish metrics endpoint is not being scraped" }

      - alert: RedishCommandErrorRatioHigh
        expr: |
          sum(rate(redish_commands_total{outcome=~"exception|error"}[5m]))
            / clamp_min(sum(rate(redish_commands_total[5m])), 1e-9) > 0.05
        for: 10m
        annotations: { summary: "More than 5% of Redish commands are failing" }

      - alert: RedishCommandExceptions
        expr: sum(increase(redish_commands_total{outcome="exception"}[5m])) > 0
        annotations: { summary: "Redish command handlers are throwing (clients receive -ERR internal server error)" }

      - alert: RedishCommandLatencyHigh
        expr: histogram_quantile(0.95, sum by (le) (rate(redish_command_duration_seconds_bucket[5m]))) > 0.05
        for: 10m
        annotations: { summary: "Redish p95 command latency above 50 ms" }

      - alert: RedishExpirationStalled
        expr: time() - max(redish_expiration_last_success_seconds) > 60
        for: 2m
        annotations: { summary: "TTL expiration sweep has not succeeded for over a minute" }

      - alert: RedishExpirationFailing
        expr: sum(increase(redish_expiration_sweeps_total{outcome="failure"}[5m])) > 0
        annotations: { summary: "TTL expiration sweeps are failing" }

      - alert: RedishExpirationOverlapping
        expr: max(redish_expiration_sweeps_active) > 1
        for: 5m
        annotations: { summary: "Sweeps take longer than their 1 s interval and are overlapping" }

      - alert: RedishThreadPoolQueueing
        expr: max(process_runtime_dotnet_thread_pool_queue_length{job="redish"}) > 100
        for: 5m
        annotations: { summary: "Redish thread pool is backing up" }

      - alert: RedishProtocolErrors
        expr: sum(increase(redisresp_parse_errors_total[15m])) > 10
        annotations: { summary: "Clients are sending input the RESP parser cannot interpret" }

      - alert: RedishAuthFailures
        expr: sum(increase(redish_auth_attempts_total{outcome="failure"}[5m])) > 20
        annotations: { summary: "Repeated AUTH failures" }

      - alert: DashboardBackendRedisErrors
        expr: sum(rate(redish_dashboard_redis_errors_total[5m])) > 0.1
        for: 5m
        annotations: { summary: "Dashboard backend cannot reach Redish" }
```

## Cardinality, privacy, and safety rules

- Every metric label comes from a small fixed set: RESP types, close reasons, outcomes, stages, the supported
  command list (others become `UNKNOWN` or `OTHER`), error codes, and exception type names. Tests assert that a
  raw command name or a payload never becomes a label.
- Client GUIDs, client addresses, and argument counts go on spans only. Key names, values, passwords, and
  connection strings appear nowhere in metrics or spans.
- Instrumentation is best-effort. Every recording call is guarded; a telemetry failure never changes command
  handling. If the Radiant host cannot start, the server keeps running and logs why.
- With no listener attached, the library and server pay an enabled check per measurement (tests cover this path).

## Production notes

- Change Grafana's admin credentials outside local development: set `GRAFANA_ADMIN_USER` and
  `GRAFANA_ADMIN_PASSWORD` in the environment when running `docker compose`. Sign-up is disabled.
- Do not expose Prometheus, Tempo, Loki, or either `/metrics` endpoint publicly; none of them authenticate.
- The compose file sets `name: redish` so its containers never collide with another stack whose compose file
  also lives in a directory named `docker/`.
- Prometheus is configured with `metric_name_validation_scheme: legacy`. Without it, Prometheus 3 negotiates
  UTF-8 names and the .NET exporter emits dotted names (`redish.commands_total`) that the dashboards do not query.
- `docker/update.sh` (or `update.bat`) pulls published images and recreates the stack without deleting volumes.

## Tests

- `src/Test.Shared/Suites/RespTelemetrySuite.cs` (library) and `RedishTelemetrySuite.cs` (server) capture
  telemetry with BCL `MeterListener`/`ActivityListener` and cover:
  - every instrument family and span, including the failure paths;
  - the Radiant host: a real Prometheus scrape, a port conflict that must not crash, settings mapping, and the
    log bridge posting to a stand-in Loki endpoint while keeping Debug lines out;
  - the no-listener path.
  Run with `dotnet run --project src/Test.Automated`, `dotnet test src/Test.Xunit`, or `dotnet test src/Test.Nunit`.
- `dashboard/server/telemetry.test.js` (Node test runner, in-memory exporters, a real ioredis client against a
  fake RESP server) covers HTTP spans and metrics, `traceparent` adoption, Redis client spans and metrics,
  failures, operation bounding, the pool gauge, and the disabled path. Run with `npm test` in `dashboard/`.
