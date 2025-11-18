# Dynamic Proxy Management

Slipka's dynamic proxy management allows you to create, configure, and manage proxy instances on-demand through REST API calls. Each proxy instance runs in isolation, perfect for parallel testing scenarios.

## Overview

Dynamic proxies are created programmatically and exist for the duration of their configured session. They provide complete isolation between test runs and allow each test to have its own proxy configuration.

## Key Features

- **On-Demand Creation**: Create proxy instances via REST API calls
- **Isolated Environments**: Each proxy runs independently with its own configuration
- **Automatic Port Allocation**: Ports are assigned dynamically from configured ranges
- **Session-Based Lifecycle**: Proxies exist for the duration of their configured session
- **Parallel Testing**: Multiple proxy instances can run simultaneously

## API Endpoints

### Create Proxy
**Endpoint:** `POST /api/Proxies`

Creates a new proxy instance with the specified configuration.

### Get Proxy Status
**Endpoint:** `GET /api/Proxies/{id}`

Retrieves the status and configuration of a proxy instance.

### Delete Proxy
**Endpoint:** `DELETE /api/Proxies/{id}`

Terminates and cleans up a proxy instance.

## Configuration

### Basic Proxy Configuration

```json
{
  "Name": "test-api-proxy",
  "TargetHost": "api.example.com",
  "TargetPort": 443,
  "TargetPortHttps": true,
  "ProxyPortHttps": false,
    "Tags": ["integration-test", "api-v2"],
    "RetainedFor": "01:00:00",
    "OpenFor": "02:00:00",
    "MaxCallsInMemory": 500
}
```

### Configuration Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Name` | string | No | Human-readable name for the proxy |
| `TargetHost` | string | Yes | Hostname or IP of the target service |
| `TargetPort` | int | No | Target service port (default: 80 or 443 if HTTPS) |
| `TargetPortHttps` | bool | No | Whether target uses HTTPS (default: false) |
| `ProxyPortHttps` | bool | No | Whether proxy should serve HTTPS (default: false) |
| `Tags` | string[] | No | Tags for categorization and reporting |
| `RetainedFor` | TimeSpan | No | How long to retain proxy data after creation |
| `OpenFor` | TimeSpan | No | How long the proxy remains active |
| `MaxCallsInMemory` | int | No | Maximum number of recent calls to keep in memory (null = unlimited) |

### Memory Management

The `MaxCallsInMemory` parameter controls memory usage by limiting the number of calls stored in memory for each dynamic proxy:

- **Purpose**: Prevents excessive memory consumption in high-traffic scenarios
- **Behavior**: Only the most recent calls are retained; older calls are automatically removed
- **Default**: `null` (unlimited - stores all calls)
- **Use Cases**: Long-running tests, high-throughput proxies, memory-constrained environments

Example: `"MaxCallsInMemory": 1000` will keep only the 1000 most recent calls in memory.

## Usage Examples

### Basic Proxy Creation

Create a simple proxy to an API service:

```bash
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "basic-api-proxy",
    "TargetHost": "jsonplaceholder.typicode.com",
    "TargetPort": 443,
    "TargetPortHttps": true
  }'
```

**Response:**
```json
{
  "id": "proxy_12345",
  "port": 61710,
  "targetHost": "jsonplaceholder.typicode.com",
  "targetPort": 443,
  "targetPortHttps": true,
  "createdAt": "2025-11-18T14:30:00Z",
  "tags": [],
  "sessionId": "session_abc123"
}
```

### Proxy with HTTPS Configuration

Create a proxy that serves HTTPS to clients but connects via HTTP to target:

```bash
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "https-proxy",
    "TargetHost": "api.internal.company.com",
    "TargetPort": 80,
    "TargetPortHttps": false,
    "ProxyPortHttps": true,
    "Tags": ["production", "internal-api"]
  }'
```

### Proxy with Traffic Manipulation

Create a proxy with injection, recording, tagging, and decorating configured:

```bash
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "full-featured-proxy",
    "TargetHost": "api.example.com",
    "TargetPort": 443,
    "TargetPortHttps": true,
    "Tags": ["test-scenario-1"],
    "InjectedCalls": [{
      "Method": "GET",
      "Uri": "/api/health",
      "StatusCode": "503",
      "Duration": 2000,
      "Response": {
        "Content": "{\"status\": \"Service Unavailable\", \"message\": \"Maintenance in progress\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Retry-After", "Values": ["3600"]}
        ]
      }
    }],
    "RecordedCalls": [{
      "Method": "POST",
      "Uri": "/api/orders"
    }],
    "TaggedCalls": [{
      "Method": "GET",
      "Uri": "/api/users/*",
      "Tags": ["user-operations", "read-heavy"]
    }],
    "Decorations": [{
      "Key": "X-Test-Session",
      "Values": ["integration-test-2025-11-18"]
    }],
    "Preprocessors": [{
      "Type": "HeaderAuthenticationPreprocessor",
      "Config": {
        "headerName": "Authorization",
        "headerValue": "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
        "uriPatterns": ["/api/protected/*"]
      }
    }]
  }'
```

### Managing Proxy Lifecycle

#### Check Proxy Status

```bash
curl -X GET http://localhost:8080/api/Proxies/proxy_12345
```

**Response:**
```json
{
  "id": "proxy_12345",
  "port": 61710,
  "status": "Running",
  "targetHost": "api.example.com",
  "targetPort": 443,
  "createdAt": "2025-11-18T14:30:00Z",
  "lastActivity": "2025-11-18T14:35:22Z",
  "requestCount": 42,
  "tags": ["test-scenario-1"],
  "sessionId": "session_abc123"
}
```

#### Delete Proxy

```bash
curl -X DELETE http://localhost:8080/api/Proxies/proxy_12345
```

**Response:**
```json
{
  "message": "Proxy proxy_12345 deleted successfully",
  "cleanupCompleted": true
}
```

## Port Management

Slipka automatically manages port allocation from configured ranges:

- **Dynamic Proxies**: Use ports from `FirstPort` to `LastPort` range (default: 61710-61800)
- **Static Proxies**: Use ports from `StaticFirstPort` to `StaticLastPort` range (default: 5000-5500)
- **Reverse Proxy**: Uses the configured `ReverseProxyPort` (default: 8080)

### Configuration Example (appsettings.json)

```json
{
  "ProxySettings": {
    "FirstPort": 61710,
    "LastPort": 61800,
    "StaticFirstPort": 5000,
    "StaticLastPort": 5500,
    "ReverseProxyPort": 8080,
    "DefaultOpenFor": "01:00:00",
    "MaxOpenFor": "2:00:00:00",
    "DefaultRetainedFor": "31:00:00:00",
    "MaxRetainedFor": "90:00:00:00"
  }
}
```

## Session Management

Each dynamic proxy is associated with a session that tracks its lifecycle:

- **Creation**: Proxy is created and assigned to a session
- **Activity**: All traffic is recorded and associated with the session
- **Retention**: Data is retained for the configured `RetainedFor` duration
- **Cleanup**: Automatic cleanup after retention period expires

### Retrieving Session Data

```bash
# Get session information
curl -X GET http://localhost:8080/api/Sessions/session_abc123

# Get recorded calls for a session
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls

# Get specific call details
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls/1
```

## Best Practices

### 1. Resource Management
- Set appropriate `OpenFor` and `RetainedFor` durations
- Clean up proxies after test completion
- Monitor resource usage in high-parallelism scenarios

### 2. Naming Conventions
- Use descriptive names that indicate test purpose
- Include test identifiers in tags for traceability

### 3. Port Planning
- Reserve sufficient port ranges for expected concurrency
- Monitor port usage in production deployments

### 4. Security Considerations
- Use HTTPS when proxying sensitive traffic
- Implement authentication for proxy management APIs
- Validate target hosts and ports

## Error Handling

Common error scenarios and responses:

### Port Exhaustion
```json
{
  "error": "No available ports in range 61710-61800",
  "code": "PORT_EXHAUSTION"
}
```

### Invalid Configuration
```json
{
  "error": "TargetHost is required",
  "code": "VALIDATION_ERROR",
  "details": ["TargetHost cannot be null or empty"]
}
```

### Proxy Not Found
```json
{
  "error": "Proxy with id 'proxy_99999' not found",
  "code": "PROXY_NOT_FOUND"
}
```

## Integration Examples

### With Testing Frameworks

#### Jest/JavaScript
```javascript
const createProxy = async (config) => {
  const response = await fetch('http://localhost:8080/api/Proxies', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(config)
  });
  return response.json();
};

// Usage in test
describe('API Integration Tests', () => {
  let proxy;

  beforeEach(async () => {
    proxy = await createProxy({
      Name: 'user-api-test',
      TargetHost: 'api.users.com',
      TargetPort: 443,
      TargetPortHttps: true,
      Tags: ['user-service', 'integration-test']
    });
  });

  afterEach(async () => {
    if (proxy?.id) {
      await fetch(`http://localhost:8080/api/Proxies/${proxy.id}`, {
        method: 'DELETE'
      });
    }
  });

  test('should handle user creation', async () => {
    // Test using proxy.port
    const response = await fetch(`http://localhost:${proxy.port}/api/users`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'John Doe' })
    });
    expect(response.status).toBe(201);
  });
});
```

#### Python (pytest)
```python
import requests
import pytest

class TestApiProxy:
    def setup_method(self):
        self.base_url = "http://localhost:8080"
        self.proxy_config = {
            "Name": "payment-api-test",
            "TargetHost": "api.payments.com",
            "TargetPort": 443,
            "TargetPortHttps": True,
            "Tags": ["payment-service", "integration-test"]
        }

    def create_proxy(self):
        response = requests.post(
            f"{self.base_url}/api/Proxies",
            json=self.proxy_config
        )
        response.raise_for_status()
        return response.json()

    def delete_proxy(self, proxy_id):
        requests.delete(f"{self.base_url}/api/Proxies/{proxy_id}")

    def test_payment_processing(self):
        proxy = self.create_proxy()
        proxy_port = proxy['port']

        try:
            # Test payment creation
            response = requests.post(
                f"http://localhost:{proxy_port}/api/payments",
                json={"amount": 100.00, "currency": "USD"}
            )
            assert response.status_code == 201

            payment = response.json()
            assert payment['status'] == 'pending'

        finally:
            self.delete_proxy(proxy['id'])
```

### CI/CD Integration

```yaml
# .github/workflows/integration-tests.yml
name: Integration Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      slipka:
        image: slipka/slipka:latest
        ports:
          - 8080:8080
        env:
          MONGODB_CONNECTION_STRING: mongodb://localhost:27017
          REDIS_CONNECTION_STRING: localhost:6379

    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Create Test Proxy
        run: |
          PROXY_RESPONSE=$(curl -X POST http://localhost:8080/api/Proxies \
            -H "Content-Type: application/json" \
            -d '{
              "Name": "ci-test-proxy",
              "TargetHost": "api.staging.company.com",
              "TargetPort": 443,
              "TargetPortHttps": true,
              "Tags": ["ci", "staging", "integration-test"]
            }')
          echo "PROXY_PORT=$(echo $PROXY_RESPONSE | jq -r '.port')" >> $GITHUB_ENV

      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration"
        env:
          TEST_PROXY_PORT: ${{ env.PROXY_PORT }}

      - name: Cleanup
        if: always()
        run: |
          curl -X DELETE http://localhost:8080/api/Proxies/$(echo $PROXY_RESPONSE | jq -r '.id')
```

## Monitoring and Troubleshooting

### Health Checks

```bash
# Check overall system health
curl -X GET http://localhost:8080/api/status

# Check proxy-specific health
curl -X GET http://localhost:8080/api/Proxies/health
```

### Common Issues

1. **Port Exhaustion**: Increase port range in configuration
2. **Connection Refused**: Verify target host/port accessibility
3. **SSL Errors**: Check certificate configuration and trust stores
4. **Timeout Issues**: Adjust timeout settings in proxy configuration

### Logging

Enable debug logging to troubleshoot proxy issues:

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Default": "Debug",
        "Slipka": "Debug"
      }
    }
  }
}
```

## Advanced Configuration

### Custom SSL Certificates

For HTTPS proxy serving, configure custom certificates:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:0",
        "Certificate": {
          "Path": "certs/proxy-cert.pfx",
          "Password": "certificate-password"
        }
      }
    }
  }
}
```

### Connection Pooling

Configure connection pooling for high-throughput scenarios:

```json
{
  "ProxySettings": {
    "MaxConnectionsPerServer": 100,
    "ConnectionTimeout": "00:00:30",
    "KeepAliveTimeout": "00:02:00"
  }
}
```

This comprehensive dynamic proxy management system provides the foundation for all of Slipka's testing capabilities, enabling isolated, configurable, and manageable proxy instances for any testing scenario.
