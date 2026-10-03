# Changelog

Notes from current version:

v0.2.0
- Observability for every component. See TELEMETRY.md for the full catalog.
  - RedisRespServer library: a Meter and ActivitySource named `RedisRespServer` (public names in `RespTelemetry`).
    Covers listener and connection lifecycle with close reasons, bytes and messages by RESP type and protocol,
    message size, dispatch latency and outcome, and parse, accept, and client-loop errors. It also opens one
    `resp.dispatch` server span per message. Emission uses the BCL only (adds a `System.Diagnostics.DiagnosticSource`
    reference for histogram bucket advice) and is a no-op until a host subscribes.
  - Redish.Server: metrics and spans for each command with `execute` and `respond` stages, error replies and
    exceptions, keyspace hits and misses, AUTH outcomes, and undelivered replies. Gauges for keys, clients by
    protocol, build info, configuration, and uptime. The TTL expiration sweep is instrumented as a background job,
    with per-stage metrics (queued, snapshot, evict), active and passive expiry, last success, overlap, and traces
    for sweeps that do work or fail. Sweep failures were previously unobserved.
  - Redish.Server hosts telemetry with Radiant 0.1.2: OTLP export, an in-process Prometheus endpoint (port 9464),
    .NET runtime metrics, and server log lines forwarded to Loki with trace correlation. Configure it in the new
    `Telemetry` section of `redish.json`. Startup failures are logged, never fatal. The server now disposes
    cleanly on shutdown.
  - Dashboard backend: OpenTelemetry HTTP server spans and `http.server.request.duration` by route template,
    Redis client spans and integration metrics, a connection-pool gauge, and Node.js runtime metrics. The Overview
    page gains an External Services card linking Grafana, Prometheus, Tempo, and Loki.
  - `docker/compose.yaml` brings up Redish, the dashboard, Prometheus, Tempo, Loki, and Grafana, with healthchecks
    and six provisioned dashboards in a Redish folder (`assets/grafana/`). `docker/update.sh` and `update.bat`
    refresh the stack.
- Telemetry test suites for the library, the server, and the dashboard backend.

v0.1.0
- Initial alpha release

Notes from previous releases:

Notes from previous releases will be pasted here.
