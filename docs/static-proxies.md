# Static Proxies

Static proxies are pre-configured, long-running proxy instances defined in the application configuration. Unlike dynamic proxies that are created on-demand, static proxies start automatically and provide consistent, always-available proxy endpoints.

## Overview

Static proxies are ideal for production monitoring, staging environments, and continuous testing scenarios where you need persistent proxy configurations.

## Configuration

Static proxies are defined in `appsettings.json`:

```json
{
  "StaticProxies": {
    "Proxies": [
      {
        "Id": "production-mirror",
        "Port": 61800,
        "TargetHost": "api.production.com",
        "TargetPort": 443,
        "TargetPortHttps": true,
        "Name": "Production API Mirror",
        "AutoStart": true,
        "MaxCallsInMemory": 1000,
        "RecordedCalls": [...],
        "InjectedCalls": [...],
        "TaggedCalls": [...],
        "Decorations": [...]
      }
    ]
  }
}
```

### Memory Management

The `MaxCallsInMemory` property controls how many recent calls are kept in memory for each static proxy. This helps manage memory usage for high-traffic proxies:

- **Type**: `integer` (nullable)
- **Default**: `null` (unlimited)
- **Purpose**: Limits the number of calls stored in memory, keeping only the most recent calls
- **Behavior**: When the limit is exceeded, older calls are automatically removed to maintain the specified maximum

Example configurations:
- `"MaxCallsInMemory": 1000` - Keep the 1000 most recent calls
- `"MaxCallsInMemory": null` - Store all calls in memory (default behavior)

## API Endpoints

- `GET /api/Proxies/static` - List all static proxies
- `GET /api/Proxies/static/{id}` - Get specific static proxy status
- `POST /api/Proxies/static/{id}/start` - Start a static proxy
- `POST /api/Proxies/static/{id}/stop` - Stop a static proxy
- `POST /api/Proxies/static/{id}/restart` - Restart a static proxy

## Usage Examples

### Production Traffic Mirroring

```json
{
  "Id": "prod-mirror",
  "Port": 61800,
  "TargetHost": "api.production.com",
  "TargetPort": 443,
  "TargetPortHttps": true,
  "Name": "Production Mirror",
  "AutoStart": true,
  "Decorations": [
    {
      "Key": "X-Environment",
      "Values": ["production"]
    }
  ],
  "RecordedCalls": [
    {
      "Method": "POST",
      "Uri": "/api/orders"
    }
  ]
}
```

### Error Simulation in Staging

```json
{
  "Id": "staging-simulator",
  "Port": 61801,
  "TargetHost": "api.staging.com",
  "Name": "Staging Error Simulator",
  "AutoStart": false,
  "InjectedCalls": [
    {
      "Method": "GET",
      "Uri": "/api/health",
      "StatusCode": "503",
      "Duration": 1000,
      "Response": {
        "Content": "{\"status\": \"Service Unavailable\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]
}
```

### API Version Testing

```json
{
  "Id": "api-v2-test",
  "Port": 5000,
  "TargetHost": "api.example.com",
  "Name": "API v2 Test Proxy",
  "AutoStart": true,
  "Decorations": [
    {
      "Key": "X-API-Version",
      "Values": ["v2"]
    }
  ]
}
```

## Management Commands

### Start Static Proxy

```bash
curl -X POST http://localhost:8080/api/Proxies/static/staging-simulator/start
```

### Stop Static Proxy

```bash
curl -X POST http://localhost:8080/api/Proxies/static/staging-simulator/stop
```

### Check Status

```bash
curl -X GET http://localhost:8080/api/Proxies/static/staging-simulator
```

Response:
```json
{
  "id": "staging-simulator",
  "port": 61801,
  "status": "Running",
  "targetHost": "api.staging.com",
  "name": "Staging Error Simulator"
}
```

## Best Practices

1. **Resource Management**: Monitor port usage and resource consumption
2. **Security**: Use appropriate authentication and authorization
3. **Monitoring**: Regularly review static proxy performance and usage
4. **Configuration Management**: Version control proxy configurations
