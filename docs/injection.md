# Injection

The Injection feature allows you to configure custom responses for specific request conditions, enabling you to simulate various failure scenarios, mock API responses, and test error handling in your applications.

## Overview

Injection intercepts requests matching specified criteria and returns custom responses instead of forwarding them to the target service. This is essential for testing how applications handle different response codes, delays, malformed responses, and other edge cases.

## Key Features

- **Conditional Response Injection**: Match requests by method, URI, headers, or body content
- **Custom Response Configuration**: Define status codes, headers, and response bodies
- **Delay Simulation**: Introduce artificial delays to test timeout handling
- **Pattern Matching**: Support for wildcards and regex in URI matching
- **Dynamic Content**: Inject responses based on request parameters

## API Endpoints

### Configure Injection
**Endpoint:** `PUT /api/Proxies/{id}/inject`

Adds injection rules to an existing proxy.

### Remove Injection
**Endpoint:** `PUT /api/Proxies/{id}/inject`

Pass an empty array to remove all injection rules.

## Configuration

### Injection Message Structure

```json
{
  "Request": {
    "Content": "optional request body pattern",
    "Headers": [
      {
        "Key": "Header-Name",
        "Values": ["expected-value"]
      }
    ]
  },
  "Response": {
    "Content": "custom response body",
    "Headers": [
      {
        "Key": "Content-Type",
        "Values": ["application/json"]
      }
    ]
  },
  "StatusCode": "503",
  "Method": "GET",
  "Uri": "/api/health",
  "Duration": 2000
}
```

### Configuration Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Request` | MessageTemplate | No | Pattern to match in incoming requests |
| `Response` | MessageTemplate | No | Custom response to inject |
| `StatusCode` | string | No | HTTP status code to return |
| `Method` | string | No | HTTP method to match (GET, POST, PUT, DELETE, etc.) |
| `Uri` | string | No | URI pattern to match (supports wildcards) |
| `Duration` | int | No | Delay in milliseconds before responding |

## Usage Examples

### Basic Error Simulation

Simulate a service outage by injecting a 503 error for health check endpoints:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/health",
      "StatusCode": "503",
      "Response": {
        "Content": "{\"status\": \"Service Unavailable\", \"message\": \"Database connection failed\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Retry-After", "Values": ["3600"]}
        ]
      }
    }
  ]'
```

### Timeout Simulation

Test timeout handling by introducing artificial delays:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/orders",
      "StatusCode": "200",
      "Duration": 5000,
      "Response": {
        "Content": "{\"orderId\": \"12345\", \"status\": \"confirmed\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]}
        ]
      }
    }
  ]'
```

### Mock API Development

Mock an API endpoint that doesn't exist yet:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/users/123",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"id\": 123, \"name\": \"John Doe\", \"email\": \"john@example.com\", \"active\": true}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Cache-Control", "Values": ["max-age=300"]}
        ]
      }
    }
  ]'
```

### Authentication Error Simulation

Simulate authentication failures:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/login",
      "StatusCode": "401",
      "Response": {
        "Content": "{\"error\": \"Invalid credentials\", \"code\": \"AUTH_FAILED\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "WWW-Authenticate", "Values": ["Bearer"]}
        ]
      }
    }
  ]'
```

### Rate Limiting Simulation

Simulate API rate limiting:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/data/*",
      "StatusCode": "429",
      "Response": {
        "Content": "{\"error\": \"Too Many Requests\", \"retryAfter\": 60}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Retry-After", "Values": ["60"]},
          {"Key": "X-RateLimit-Remaining", "Values": ["0"]},
          {"Key": "X-RateLimit-Reset", "Values": ["1637000000"]}
        ]
      }
    }
  ]'
```

### Malformed Response Testing

Test how clients handle malformed JSON responses:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/config",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"config\": {\"database\": \"prod\", \"timeout\": 30, \"features\": [\"feature1\", \"feature2\"]",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]}
        ]
      }
    }
  ]'
```

### Conditional Injection Based on Headers

Inject responses only for requests with specific headers:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/admin/*",
      "Request": {
        "Headers": [
          {"Key": "Authorization", "Values": ["Bearer invalid-token"]}
        ]
      },
      "StatusCode": "403",
      "Response": {
        "Content": "{\"error\": \"Forbidden\", \"message\": \"Invalid or expired token\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]}
        ]
      }
    }
  ]'
```

### Dynamic Response Based on Request Body

Inject responses based on request content:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/webhook",
      "Request": {
        "Content": "*\"eventType\": \"user.created\"*"
      },
      "StatusCode": "200",
      "Response": {
        "Content": "{\"status\": \"processed\", \"webhookId\": \"wh_12345\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]}
        ]
      }
    }
  ]'
```

## Pattern Matching

### URI Patterns

Slipka supports flexible URI pattern matching:

- **Exact Match**: `/api/users/123`
- **Wildcard**: `/api/users/*` (matches `/api/users/123`, `/api/users/456`, etc.)
- **Path Segments**: `/api/*/config` (matches `/api/service1/config`, `/api/service2/config`)
- **Multiple Wildcards**: `/api/*/*/status`

### Header Matching

Headers can be matched exactly or with wildcards:

```json
{
  "Request": {
    "Headers": [
      {
        "Key": "User-Agent",
        "Values": ["*Chrome*"]
      },
      {
        "Key": "X-API-Version",
        "Values": ["v2.*"]
      }
    ]
  }
}
```

### Content Matching

Request body content can be matched using wildcards:

```json
{
  "Request": {
    "Content": "*\"action\": \"delete\"*"
  }
}
```

## Multiple Injection Rules

You can configure multiple injection rules that are evaluated in order:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/health",
      "StatusCode": "503",
      "Response": {
        "Content": "{\"status\": \"Service Unavailable\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    },
    {
      "Method": "GET",
      "Uri": "/api/status",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"status\": \"OK\", \"version\": \"1.2.3\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    },
    {
      "Method": "POST",
      "Uri": "/api/*",
      "StatusCode": "500",
      "Duration": 1000,
      "Response": {
        "Content": "{\"error\": \"Internal Server Error\", \"code\": \"GENERIC_ERROR\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'
```

## Integration with Static Proxies

Injection can also be configured in static proxy definitions in `appsettings.json`:

```json
{
  "StaticProxies": {
    "Proxies": [
      {
        "Id": "api-simulator",
        "Port": 5000,
        "TargetHost": "api.example.com",
        "TargetPort": 443,
        "Name": "API Simulator",
        "AutoStart": true,
        "InjectedCalls": [
          {
            "Method": "GET",
            "Uri": "/api/health",
            "StatusCode": "503",
            "Duration": 1000,
            "Response": {
              "Content": "{\"status\": \"Service Unavailable\", \"maintenance\": true}",
              "Headers": [
                {"Key": "Content-Type", "Values": ["application/json"]},
                {"Key": "Retry-After", "Values": ["1800"]}
              ]
            }
          }
        ]
      }
    ]
  }
}
```

## Testing Scenarios

### Microservice Chaos Testing

```bash
# Simulate cascading failures
curl -X PUT http://localhost:8080/api/Proxies/user-service/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/users/*",
      "StatusCode": "504",
      "Duration": 30000,
      "Response": {
        "Content": "{\"error\": \"Gateway Timeout\", \"service\": \"user-service\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'

curl -X PUT http://localhost:8080/api/Proxies/order-service/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/orders",
      "StatusCode": "502",
      "Response": {
        "Content": "{\"error\": \"Bad Gateway\", \"service\": \"order-service\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'
```

### API Version Compatibility Testing

```bash
# Simulate deprecated API version
curl -X PUT http://localhost:8080/api/Proxies/api-v1/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/v1/*",
      "StatusCode": "410",
      "Response": {
        "Content": "{\"error\": \"API Version Deprecated\", \"migrateTo\": \"v2\", \"sunsetDate\": \"2025-12-31\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Deprecation", "Values": ["true"]},
          {"Key": "Link", "Values": ["</v2/api>; rel=\"successor-version\""]}
        ]
      }
    }
  ]'
```

### Network Condition Simulation

```bash
# Simulate slow network
curl -X PUT http://localhost:8080/api/Proxies/mobile-api/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/mobile/*",
      "StatusCode": "200",
      "Duration": 5000,
      "Response": {
        "Content": "{\"data\": \"mobile-optimized-content\", \"compressed\": true}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "Content-Encoding", "Values": ["gzip"]}
        ]
      }
    }
  ]'
```

### Security Testing

```bash
# Simulate security vulnerabilities
curl -X PUT http://localhost:8080/api/Proxies/vulnerable-api/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/internal/*",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"sensitive\": \"data\", \"apiKeys\": \"exposed\", \"internalConfig\": \"visible\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'
```

## Advanced Features

### Dynamic Response Generation

Use placeholders for dynamic content:

```bash
curl -X PUT http://localhost:8080/api/Proxies/dynamic-api/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/echo/*",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"path\": \"{{request.uri}}\", \"timestamp\": \"{{timestamp}}\", \"requestId\": \"{{requestId}}\"}",
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/json"]},
          {"Key": "X-Request-ID", "Values": ["{{requestId}}"]},
          {"Key": "X-Timestamp", "Values": ["{{timestamp}}"]}
        ]
      }
    }
  ]'
```

### Conditional Logic

Implement conditional responses based on request parameters:

```bash
curl -X PUT http://localhost:8080/api/Proxies/smart-api/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/feature-flag",
      "Request": {
        "Headers": [
          {"Key": "X-User-Plan", "Values": ["free"]}
        ]
      },
      "StatusCode": "200",
      "Response": {
        "Content": "{\"featureEnabled\": false, \"upgradeRequired\": true}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    },
    {
      "Method": "GET",
      "Uri": "/api/feature-flag",
      "Request": {
        "Headers": [
          {"Key": "X-User-Plan", "Values": ["premium"]}
        ]
      },
      "StatusCode": "200",
      "Response": {
        "Content": "{\"featureEnabled\": true, \"plan\": \"premium\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'
```

## Best Practices

### 1. Testing Strategy
- Start with simple injections and gradually add complexity
- Test both positive and negative scenarios
- Use realistic response times and content

### 2. Pattern Design
- Use specific patterns before general ones
- Document injection rules for team members
- Regularly review and update injection configurations

### 3. Performance Considerations
- Minimize the number of active injection rules
- Use efficient pattern matching
- Monitor proxy performance with injections active

### 4. Maintenance
- Clean up injection rules after testing
- Version control injection configurations
- Document expected behavior for each injection

## Troubleshooting

### Common Issues

1. **Injection Not Triggering**
   - Verify pattern matching syntax
   - Check request headers and body format
   - Ensure injection rules are properly configured

2. **Unexpected Behavior**
   - Review rule precedence (first match wins)
   - Check for conflicting patterns
   - Validate JSON syntax in configurations

3. **Performance Impact**
   - Monitor request processing times
   - Reduce number of active rules
   - Use more specific patterns

### Debugging

Enable debug logging to troubleshoot injection issues:

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Slipka.Proxy": "Debug",
        "Slipka": "Debug"
      }
    }
  }
}
```

Check injection rule status:

```bash
curl -X GET http://localhost:8080/api/Proxies/proxy_12345
```

## Integration Examples

### Load Testing with Injection

```bash
# Create proxy with failure injection
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "load-test-proxy",
    "TargetHost": "api.example.com",
    "TargetPort": 443,
    "TargetPortHttps": true,
    "InjectedCalls": [{
      "Method": "GET",
      "Uri": "/api/heavy-computation",
      "StatusCode": "200",
      "Duration": 10000,
      "Response": {
        "Content": "{\"result\": \"computed\", \"processingTime\": 10000}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }]
  }'
```

### Contract Testing

```bash
# Mock provider responses for consumer-driven contract testing
curl -X PUT http://localhost:8080/api/Proxies/contract-test/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/contracts/123",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"contractId\": \"123\", \"status\": \"active\", \"terms\": {\"duration\": 365, \"value\": 10000}}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }
  ]'
```

The Injection feature provides powerful capabilities for simulating various scenarios that applications might encounter in production, making it an essential tool for comprehensive testing strategies.
