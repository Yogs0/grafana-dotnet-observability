# Telemetry Data Access Guide

This document describes how to access logs, traces, and metrics in your observability stack.

## Quick Access

| Data Type | UI Access | Direct API |
|-----------|-----------|------------|
| **Logs** | [Grafana → Loki](http://localhost:3000/explore?orgId=1&left=%5B%22now-1h%22,%22now%22,%22Loki%22%5D) | `http://localhost:3100` |
| **Traces** | [Grafana → Tempo](http://localhost:3000/explore?orgId=1&left=%5B%22now-1h%22,%22now%22,%22Tempo%22%5D) | `http://localhost:3200` |
| **Metrics** | [Grafana → Prometheus](http://localhost:3000/explore?orgId=1&left=%5B%22now-1h%22,%22now%22,%22Prometheus%22%5D) | `http://localhost:9090` |

**Grafana Credentials:** `admin` / `admin`

---

## Grafana UI (Recommended)

### Access
- **URL:** http://localhost:3000
- **Username:** `admin`
- **Password:** `admin`

### Navigation
1. Click **Explore** (compass icon on left sidebar)
2. Select datasource from dropdown:
   - **Loki** for logs
   - **Tempo** for traces
   - **Prometheus** for metrics

---

## Logs (Loki)

### Grafana Query Examples

```logql
# All logs from your service
{service_name="OpenTelemetryExample"}

# Filter by log level
{service_name="OpenTelemetryExample"} | json | level="error"

# Search for specific text
{service_name="OpenTelemetryExample"} |= "Exception"

# Filter by trace ID
{service_name="OpenTelemetryExample"} | json | trace_id="abc123"
```

### Direct API Access

```bash
# Query logs from last hour
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="OpenTelemetryExample"}' \
  --data-urlencode "start=$(date -d '1 hour ago' +%s)000000000" \
  --data-urlencode "end=$(date +%s)000000000" \
  --data-urlencode "limit=100"

# Get all label values
curl "http://localhost:3100/loki/api/v1/labels"

# Get values for a specific label
curl "http://localhost:3100/loki/api/v1/label/service_name/values"
```

### Storage Location
- **Container path:** `/loki/chunks/`
- **Docker volume:** `loki-data`
- **Retention:** 10 years

---

## Traces (Tempo)

### Grafana Query Examples

**Search tab:**
- Service Name: `OpenTelemetryExample`
- Operation: (leave empty for all)
- Tags: `http.status_code=500`

**TraceQL tab:**
```
# Find traces by service
{resource.service.name="OpenTelemetryExample"}

# Find error traces
{status=error}

# Find slow traces (>1s)
{resource.service.name="OpenTelemetryExample" && duration>1s}

# Find by HTTP status
{span.http.status_code=500}
```

### Direct API Access

```bash
# Get trace by ID
curl "http://localhost:3200/api/traces/{traceID}"

# Search traces
curl "http://localhost:3200/api/search?service.name=OpenTelemetryExample&limit=20"

# Check Tempo status
curl "http://localhost:3200/ready"
```

### Storage Location
- **Container path:** `/tmp/tempo/blocks/`
- **Docker volume:** `tempo-data`
- **Retention:** 10 years
- **Sampling:** 10% of traces + all errors + slow traces (>2s)

---

## Metrics (Prometheus)

### Grafana Query Examples

```promql
# HTTP request count
otel_http_server_request_duration_seconds_count{service_name="OpenTelemetryExample"}

# Request rate (per second)
rate(otel_http_server_request_duration_seconds_count{service_name="OpenTelemetryExample"}[5m])

# Average request duration
rate(otel_http_server_request_duration_seconds_sum[5m]) / rate(otel_http_server_request_duration_seconds_count[5m])

# Error rate
sum(rate(otel_http_server_request_duration_seconds_count{http_status_code=~"5.."}[5m])) / sum(rate(otel_http_server_request_duration_seconds_count[5m]))

# Custom metrics from your app
otel_weather_forecast_requests_total
otel_weather_forecast_generation_duration_seconds
```

### Direct API Access

```bash
# Instant query
curl "http://localhost:9090/api/v1/query?query=up"

# Range query (last hour)
curl "http://localhost:9090/api/v1/query_range?query=up&start=$(date -d '1 hour ago' +%s)&end=$(date +%s)&step=60"

# Get all metric names
curl "http://localhost:9090/api/v1/label/__name__/values"

# Get targets (scrape status)
curl "http://localhost:9090/api/v1/targets"
```

### Storage Location
- **Container path:** `/prometheus/`
- **Docker volume:** `prometheus-data`
- **Retention:** 10 years (or 50GB max, whichever comes first)

---

## Physical Storage Locations

### Check Docker Volume Paths

```bash
# Loki data
docker volume inspect opentelemetryexample_loki-data

# Tempo data
docker volume inspect opentelemetryexample_tempo-data

# Prometheus data
docker volume inspect opentelemetryexample_prometheus-data

# Grafana data (dashboards, settings)
docker volume inspect opentelemetryexample_grafana-data
```

### Windows Host Paths (WSL2/Docker Desktop)

| Volume | Approximate Path |
|--------|------------------|
| Loki | `\\wsl$\docker-desktop-data\data\docker\volumes\*_loki-data\_data` |
| Tempo | `\\wsl$\docker-desktop-data\data\docker\volumes\*_tempo-data\_data` |
| Prometheus | `\\wsl$\docker-desktop-data\data\docker\volumes\*_prometheus-data\_data` |

---

## Backup & Export

### Export Logs

```bash
# Export to JSON file
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="OpenTelemetryExample"}' \
  --data-urlencode "start=$(date -d '30 days ago' +%s)000000000" \
  --data-urlencode "end=$(date +%s)000000000" \
  --data-urlencode "limit=10000" \
  > logs_export.json
```

### Export Metrics

```bash
# Prometheus snapshot (creates backup in /prometheus/snapshots/)
curl -X POST "http://localhost:9090/api/v1/admin/tsdb/snapshot"
```

### Backup Docker Volumes

```bash
# Stop containers first
docker-compose stop

# Backup volumes
docker run --rm -v opentelemetryexample_loki-data:/data -v $(pwd):/backup alpine tar czf /backup/loki-backup.tar.gz /data
docker run --rm -v opentelemetryexample_tempo-data:/data -v $(pwd):/backup alpine tar czf /backup/tempo-backup.tar.gz /data
docker run --rm -v opentelemetryexample_prometheus-data:/data -v $(pwd):/backup alpine tar czf /backup/prometheus-backup.tar.gz /data

# Restart
docker-compose start
```

---

## Correlation: Linking Logs ↔ Traces ↔ Metrics

Grafana is pre-configured to link data across all three datasources:

1. **From a Trace:** Click on a span → "Logs for this span" shows related logs
2. **From Logs:** If a log contains `trace_id`, click it to jump to the trace
3. **From Metrics:** Exemplars (if enabled) link to specific traces

### Manual Correlation

If you have a trace ID, query each system:

```bash
# Trace
curl "http://localhost:3200/api/traces/{traceID}"

# Related logs
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="OpenTelemetryExample"} |= "{traceID}"'
```

---

## Retention Configuration

| System | Retention | Max Size | Config File |
|--------|-----------|----------|-------------|
| Loki | 10 years | No hard limit | `loki-config.yaml` |
| Tempo | 10 years | No hard limit | `tempo-config.yaml` |
| Prometheus | 10 years | 50GB | `docker-compose.yaml` |

**Total estimated storage:** ~200GB max
