# Health Checks

Slipka provides comprehensive health monitoring for all system components including MongoDB, Redis, and proxy instances to ensure reliable operation.

## Overview

Health checks validate the operational status of all Slipka dependencies and components, providing early detection of issues and supporting automated monitoring systems.

## Health Check Endpoints

### System Health
**Endpoint:** `GET /api/status`

Returns overall system health status.

### MongoDB Health
**Endpoint:** `GET /api/status/mongodb`

Checks MongoDB connection and basic operations.

### Redis Health
**Endpoint:** `GET /api/status/redis`

Validates Redis connectivity and operations.

### Proxy Health
**Endpoint:** `GET /api/status/proxy`

Checks proxy instance availability.

### Static Proxy Health
**Endpoint:** `GET /api/status/static-proxy`

Validates static proxy health.

## Configuration

Health checks are configured in `Startup.cs` and can be customized:

```csharp
// Add health checks
services.AddHealthChecks()
    .AddMongoDb(mongodbSettings.ConnectionString, name: "mongodb")
    .AddRedis(redisSettings.ConnectionString, name: "redis")
    .AddCheck<StaticProxyHealthCheck>("static-proxy")
    .AddCheck<ReverseProxyHealthCheck>("reverse-proxy");
```

## Usage Examples

### Check Overall System Health

```bash
curl -X GET http://localhost:8080/api/status
```

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "mongodb": {
      "status": "Healthy",
      "description": "MongoDB is healthy",
      "duration": "00:00:00.0456789"
    },
    "redis": {
      "status": "Healthy",
      "description": "Redis is healthy",
      "duration": "00:00:00.0234567"
    },
    "static-proxy": {
      "status": "Healthy",
      "description": "All static proxies are running",
      "duration": "00:00:00.0543210"
    }
  }
}
```

### Check Individual Components

```bash
# MongoDB health
curl -X GET http://localhost:8080/api/status/mongodb

# Redis health
curl -X GET http://localhost:8080/api/status/redis

# Proxy health
curl -X GET http://localhost:8080/api/status/proxy
```

### Health Check Response Codes

- **200 OK**: All components healthy
- **503 Service Unavailable**: One or more components unhealthy
- **500 Internal Server Error**: Health check execution failed

## Custom Health Checks

### Implementing Custom Health Checks

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class CustomProxyHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform custom health check logic
            bool isHealthy = await CheckProxyHealth();

            return isHealthy
                ? HealthCheckResult.Healthy("Custom proxy is healthy")
                : HealthCheckResult.Unhealthy("Custom proxy is unhealthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
```

### Register Custom Health Check

```csharp
services.AddHealthChecks()
    .AddCheck<CustomProxyHealthCheck>("custom-proxy");
```

## Monitoring Integration

### Prometheus Integration

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'slipka'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: '/api/status/metrics'
```

### Grafana Dashboard

Create dashboards to visualize health metrics over time.

### Alerting Rules

```yaml
# alert_rules.yml
groups:
  - name: slipka
    rules:
      - alert: SlipkaUnhealthy
        expr: slipka_health_status != 1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Slipka health check failed"
```

## Best Practices

1. **Monitoring**: Set up automated monitoring of health endpoints
2. **Alerting**: Configure alerts for health check failures
3. **Dependencies**: Monitor all critical dependencies
4. **Performance**: Health checks should be lightweight and fast
5. **Logging**: Log health check failures for debugging
