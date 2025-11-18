# Structured Logging

Slipka provides comprehensive structured logging capabilities with correlation ID support for distributed tracing and debugging across complex systems.

## Overview

The logging system integrates with Serilog and provides correlation ID injection, structured events, and configurable log levels for different components.

## API Endpoint

**Endpoint:** `POST /api/logging`

Logs structured messages with correlation tracking.

## Configuration

Logging is configured in `appsettings.json`:

```json
{
  "Logging": {
    "IncludeScopes": false,
    "Debug": {
      "LogLevel": {
        "Default": "Warning"
      }
    },
    "Console": {
      "LogLevel": {
        "Default": "Warning"
      }
    }
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Slipka": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/slipka-.log",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

## Usage Examples

### Log Application Events

```bash
curl -X POST http://localhost:8080/api/logging \
  -H "Content-Type: application/json" \
  -d '{
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440000",
    "Level": "Information",
    "Message": "User authentication successful",
    "Timestamp": "2025-11-18T14:30:00Z",
    "Properties": {
      "UserId": "user123",
      "LoginMethod": "oauth",
      "IPAddress": "192.168.1.100"
    }
  }'
```

### Log Errors with Context

```bash
curl -X POST http://localhost:8080/api/logging \
  -H "Content-Type: application/json" \
  -d '{
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440001",
    "Level": "Error",
    "Message": "Database connection failed",
    "Timestamp": "2025-11-18T14:35:00Z",
    "Properties": {
      "Database": "userdb",
      "Operation": "SELECT",
      "Duration": 5000,
      "ErrorCode": "CONNECTION_TIMEOUT",
      "RetryCount": 3
    }
  }'
```

### Log Performance Metrics

```bash
curl -X POST http://localhost:8080/api/logging \
  -H "Content-Type: application/json" \
  -d '{
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440002",
    "Level": "Information",
    "Message": "API request completed",
    "Timestamp": "2025-11-18T14:40:00Z",
    "Properties": {
      "Endpoint": "/api/users",
      "Method": "GET",
      "StatusCode": 200,
      "Duration": 245,
      "ResponseSize": 1024,
      "CacheHit": true
    }
  }'
```

## Log Levels

- **Trace**: Detailed diagnostic information
- **Debug**: Debugging information
- **Information**: General information messages
- **Warning**: Warning messages for potential issues
- **Error**: Error messages for failures
- **Fatal**: Critical errors that cause application failure

## Correlation ID Format

Correlation IDs should follow UUID format:
- Format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- Example: `550e8400-e29b-41d4-a716-446655440000`

## Integration Examples

### Application Integration

```javascript
const logger = {
  log: async (level, message, correlationId, properties = {}) => {
    const logEntry = {
      CorrelationId: correlationId,
      Level: level,
      Message: message,
      Timestamp: new Date().toISOString(),
      Properties: properties
    };

    await fetch('http://localhost:8080/api/logging', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(logEntry)
    });
  }
};

// Usage
const correlationId = '550e8400-e29b-41d4-a716-446655440000';
await logger.log('Information', 'User login initiated', correlationId, {
  userId: 'user123',
  loginMethod: 'password'
});
```

### Middleware Integration

```javascript
// Express.js middleware for automatic logging
const loggingMiddleware = (req, res, next) => {
  const correlationId = req.headers['x-correlation-id'] ||
                       req.headers['x-request-id'] ||
                       generateCorrelationId();

  const startTime = Date.now();

  // Add correlation ID to response
  res.setHeader('X-Correlation-ID', correlationId);

  res.on('finish', async () => {
    const duration = Date.now() - startTime;

    await fetch('http://localhost:8080/api/logging', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        CorrelationId: correlationId,
        Level: res.statusCode >= 400 ? 'Warning' : 'Information',
        Message: `HTTP ${req.method} ${req.path} completed`,
        Timestamp: new Date().toISOString(),
        Properties: {
          method: req.method,
          path: req.path,
          statusCode: res.statusCode,
          duration: duration,
          userAgent: req.get('User-Agent'),
          ipAddress: req.ip
        }
      })
    });
  });

  next();
};
```

## Log Analysis

### Filter by Correlation ID

```bash
# Search logs for specific correlation ID
grep "550e8400-e29b-41d4-a716-446655440000" logs/slipka-*.log
```

### Performance Analysis

```bash
# Find slow requests
grep "Duration.*[0-9]\{4,\}" logs/slipka-*.log | jq '.Properties.Duration'
```

### Error Analysis

```bash
# Count errors by type
grep '"Level": "Error"' logs/slipka-*.log | \
  jq '.Properties.ErrorCode' | \
  sort | uniq -c | sort -nr
```

## Best Practices

1. **Correlation IDs**: Always include correlation IDs for request tracing
2. **Structured Data**: Use properties for searchable data, message for human-readable text
3. **Log Levels**: Use appropriate levels for different types of events
4. **Performance**: Avoid logging sensitive data or large objects
5. **Retention**: Configure appropriate log retention policies
