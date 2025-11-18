# Reverse Proxy

The reverse proxy provides multi-route HTTP request routing for complex microservice architectures, directing traffic to different backend services based on URL paths.

## Overview

Reverse proxy routes incoming requests to multiple backend services based on configurable path patterns, enabling API gateway functionality with traffic manipulation capabilities.

## Configuration

Reverse proxy is configured in `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Port": 8080,
    "EnableHttps": false,
    "Routes": [
      {
        "Id": "service1",
        "Path": "/api/service1/*",
        "TargetHost": "localhost",
        "TargetPort": 3000,
        "TargetHttps": false,
        "RequiresAuthentication": false,
        "MaxCallsInMemory": 2000,
        "Decorations": [...],
        "RecordedCalls": [...],
        "InjectedCalls": [...],
        "Preprocessors": [...],
        "ErrorMasking": {
          "Enabled": true,
          "MaxErrorResponseLength": 1000,
          "ErrorResponseRegex": "(?i)(stack\\s*trace|exception|error\\s*details)",
          "MaskedErrorMessage": "An internal server error occurred. Please check the application logs for more details.",
          "ErrorStatusCodes": [500, 502, 503, 504]
        }
      }
    ]
  }
}
```

## Error Masking

Error masking prevents sensitive error details like stack traces from being exposed to clients while preserving HTTP status codes and logging full error information with correlation IDs for debugging.

### Configuration

Configure error masking per route to control which errors are masked and how:

```json
{
  "ErrorMasking": {
    "Enabled": true,
    "MaxErrorResponseLength": 1000,
    "ErrorResponseRegex": "(?i)(stack\\s*trace|exception|error\\s*details)",
    "MaskedErrorMessage": "An internal server error occurred. Please check the application logs for more details.",
    "ErrorStatusCodes": [500, 502, 503, 504]
  }
}
```

## Memory Management

Each reverse proxy route can be configured with memory limits to control resource usage:

```json
{
  "Id": "high-traffic-service",
  "Path": "/api/busy/*",
  "TargetHost": "busy-service",
  "TargetPort": 8080,
  "MaxCallsInMemory": 5000
}
```

### Configuration

- **`MaxCallsInMemory`**: Maximum number of recent calls to keep in memory per route
  - **Type**: `integer` (nullable)
  - **Default**: `null` (unlimited)
  - **Purpose**: Prevents memory exhaustion from high-traffic routes
  - **Behavior**: Automatically removes oldest calls when limit is exceeded

## Error Masking

### Configuration

Configure error masking per route to control which errors are masked and how:
- **`MaxErrorResponseLength`**: Maximum character length before masking (optional)
- **`ErrorResponseRegex`**: Regex pattern to match error responses for masking (optional)
- **`MaskedErrorMessage`**: Message to return instead of the original error
- **`ErrorStatusCodes`**: HTTP status codes to apply masking to (defaults: 500, 502, 503, 504)

### Behavior

When error masking is triggered:
1. **Status code is preserved** - No change to HTTP response codes
2. **Response content is replaced** - Original error details are hidden
3. **Correlation IDs are included** - Both correlation ID and sub-ID are added to response headers and masked message
4. **Full error is logged** - Complete error details are logged with correlation IDs for debugging

### Masked Response Example

```
An internal server error occurred. Please check the application logs for more details.
Correlation ID: 12345678-abcd-1234-5678-123456789abc
Correlation Sub ID: 87654321-dcba-4321-8765-cba987654321
```

### Use Cases

- **Production Safety**: Prevent stack traces from leaking to external clients
- **Security**: Avoid exposing internal system details in error responses
- **Compliance**: Meet security requirements for error message sanitization
- **Debugging**: Maintain full error visibility in logs with correlation tracking

## API Endpoints

- `GET /api/Proxies/reverse` - Get reverse proxy status
- `POST /api/Proxies/reverse/start` - Start reverse proxy
- `POST /api/Proxies/reverse/stop` - Stop reverse proxy

## Route Configuration

### Basic Route

```json
{
  "Id": "user-service",
  "Path": "/api/users/*",
  "TargetHost": "user-service.internal",
  "TargetPort": 8080,
  "TargetHttps": false,
  "RequiresAuthentication": true
}
```

### Microservice Architecture

```json
{
  "Routes": [
    {
      "Id": "user-service",
      "Path": "/api/users/*",
      "TargetHost": "user-service",
      "TargetPort": 3000
    },
    {
      "Id": "order-service",
      "Path": "/api/orders/*",
      "TargetHost": "order-service",
      "TargetPort": 3001
    },
    {
      "Id": "payment-service",
      "Path": "/api/payments/*",
      "TargetHost": "payment-service",
      "TargetPort": 3002,
      "RequiresAuthentication": true
    },
    {
      "Id": "default",
      "Path": "/*",
      "TargetHost": "frontend-service",
      "TargetPort": 3003
    }
  ]
}
```

## Usage Examples

### Start Reverse Proxy

```bash
curl -X POST http://localhost:8080/api/Proxies/reverse/start
```

### Stop Reverse Proxy

```bash
curl -X POST http://localhost:8080/api/Proxies/reverse/stop
```

### Check Status

```bash
curl -X GET http://localhost:8080/api/Proxies/reverse
```

### Route Traffic

```bash
# Routes to user-service
curl -X GET http://localhost:8080/api/users/123

# Routes to order-service
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"userId": 123, "amount": 99.99}'

# Routes to payment-service
curl -X POST http://localhost:8080/api/payments/process \
  -H "Authorization: Bearer token" \
  -H "Content-Type: application/json" \
  -d '{"orderId": "order_123", "amount": 99.99}'
```

## Advanced Features

### Path Rewriting

Routes support path rewriting for clean API design:

```json
{
  "Id": "api-v2",
  "Path": "/v2/*",
  "TargetHost": "api-service",
  "TargetPort": 8080,
  "PathRewrite": {
    "From": "^/v2/(.*)",
    "To": "/api/$1"
  }
}
```

### Load Balancing

```json
{
  "Id": "load-balanced-service",
  "Path": "/api/balanced/*",
  "Targets": [
    {"Host": "service-1", "Port": 8080, "Weight": 60},
    {"Host": "service-2", "Port": 8080, "Weight": 40}
  ],
  "LoadBalancing": "WeightedRoundRobin"
}
```

### Circuit Breaker

```json
{
  "Id": "protected-service",
  "Path": "/api/protected/*",
  "TargetHost": "unstable-service",
  "TargetPort": 8080,
  "CircuitBreaker": {
    "FailureThreshold": 5,
    "RecoveryTimeout": 30000,
    "MonitoringPeriod": 60000
  }
}
```

## Best Practices

1. **Route Design**: Use clear, hierarchical path patterns
2. **Security**: Enable authentication for sensitive routes
3. **Monitoring**: Track route performance and errors
4. **Scalability**: Design routes for horizontal scaling
