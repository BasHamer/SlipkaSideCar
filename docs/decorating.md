# Decorating

The Decorating feature automatically adds custom headers to all HTTP traffic passing through Slipka proxies, enabling request tracing, observability, and traffic identification across distributed systems.

## Overview

Decorators inject headers into requests before they are forwarded to target services, providing essential metadata for logging, monitoring, debugging, and distributed tracing. This ensures that all proxied traffic can be easily identified and correlated across multiple services and systems.

## Key Features

- **Automatic Header Injection**: Add custom headers to all proxied requests
- **Correlation ID Generation**: Inject unique identifiers for request tracing
- **Environment Identification**: Mark traffic by environment (test, staging, production)
- **Service Tagging**: Identify traffic source and routing information
- **Distributed Tracing**: Support for trace propagation across service boundaries
- **Configurable Headers**: Define any number of custom header/value pairs

## API Endpoints

### Configure Decorating
**Endpoint:** `PUT /api/Proxies/{id}/decorate`

Adds decoration headers to an existing proxy.

### Remove Decorating
**Endpoint:** `PUT /api/Proxies/{id}/decorate`

Pass an empty array to remove all decoration headers.

## Configuration

### Decoration Message Structure

```json
{
  "Key": "X-Request-Source",
  "Values": ["slipka-proxy", "integration-test"]
}
```

### Configuration Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Key` | string | Yes | Header name to add |
| `Values` | string[] | Yes | Array of header values |

## Usage Examples

### Basic Traffic Source Identification

Mark all traffic as coming through Slipka:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Proxy-Source",
      "Values": ["slipka"]
    }
  ]'
```

### Environment and Test Identification

Identify the environment and test context:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Environment",
      "Values": ["testing"]
    },
    {
      "Key": "X-Test-Session",
      "Values": ["integration-test-2025-11-18"]
    },
    {
      "Key": "X-Test-ID",
      "Values": ["test-run-abc123"]
    }
  ]'
```

### Correlation ID Injection

Add unique correlation IDs for request tracing:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Correlation-ID",
      "Values": ["{{correlationId}}"]
    },
    {
      "Key": "X-Request-ID",
      "Values": ["{{requestId}}"]
    }
  ]'
```

### Service and Route Identification

Identify which services and routes traffic is using:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Service-Chain",
      "Values": ["test-client", "slipka-proxy", "api-server"]
    },
    {
      "Key": "X-Route",
      "Values": ["api-v2"]
    },
    {
      "Key": "X-Proxy-Version",
      "Values": ["2.1.0"]
    }
  ]'
```

### User Context Propagation

Propagate user context information:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-User-Type",
      "Values": ["test-user"]
    },
    {
      "Key": "X-User-Plan",
      "Values": ["premium"]
    },
    {
      "Key": "X-Client-Version",
      "Values": ["1.2.3"]
    }
  ]'
```

### Geographic and Infrastructure Context

Add geographic and infrastructure metadata:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Data-Center",
      "Values": ["us-west-2"]
    },
    {
      "Key": "X-Cluster",
      "Values": ["test-cluster"]
    },
    {
      "Key": "X-Infrastructure",
      "Values": ["kubernetes", "aws"]
    }
  ]'
```

## Static Proxy Decoration Configuration

Decorations can be configured in static proxy definitions:

```json
{
  "StaticProxies": {
    "Proxies": [
      {
        "Id": "production-mirror",
        "Port": 61800,
        "TargetHost": "api.production.com",
        "TargetPort": 443,
        "Name": "Production API Mirror",
        "AutoStart": true,
        "Decorations": [
          {
            "Key": "X-Proxy-Source",
            "Values": ["slipka-production-mirror"]
          },
          {
            "Key": "X-Environment",
            "Values": ["production"]
          },
          {
            "Key": "X-Correlation-ID",
            "Values": ["{{correlationId}}"]
          }
        ]
      }
    ]
  }
}
```

## Advanced Decoration Scenarios

### Dynamic Value Injection

Use placeholders for dynamic values:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Timestamp",
      "Values": ["{{timestamp}}"]
    },
    {
      "Key": "X-Request-Method",
      "Values": ["{{method}}"]
    },
    {
      "Key": "X-Request-URI",
      "Values": ["{{uri}}"]
    }
  ]'
```

### Multi-Value Headers

Add multiple values to the same header:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Feature-Flags",
      "Values": ["feature-a", "feature-b", "experimental-c"]
    },
    {
      "Key": "X-Supported-API-Versions",
      "Values": ["v1", "v2", "v3"]
    }
  ]'
```

### Conditional Decoration Based on Context

While basic decoration applies to all requests, you can combine with injection for conditional decoration:

```bash
# First, set up basic decoration for all requests
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Base-Context",
      "Values": ["test-traffic"]
    }
  ]'

# Then use injection to add conditional headers
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/inject \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/admin/*",
      "StatusCode": "200",
      "Response": {
        "Content": "{\"admin\": true}",
        "Headers": [
          {"Key": "X-User-Role", "Values": ["admin"]},
          {"Key": "X-Elevated-Access", "Values": ["true"]}
        ]
      }
    }
  ]'
```

## Integration with Distributed Tracing

### OpenTelemetry Integration

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "traceparent",
      "Values": ["00-{{traceId}}-{{spanId}}-01"]
    },
    {
      "Key": "tracestate",
      "Values": ["vendor=slipka"]
    }
  ]'
```

### Jaeger Integration

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "uber-trace-id",
      "Values": ["{{traceId}}:{{spanId}}:0:1"]
    },
    {
      "Key": "jaeger-debug-id",
      "Values": ["{{requestId}}"]
    }
  ]'
```

### Zipkin Integration

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-B3-TraceId",
      "Values": ["{{traceId}}"]
    },
    {
      "Key": "X-B3-SpanId",
      "Values": ["{{spanId}}"]
    },
    {
      "Key": "X-B3-ParentSpanId",
      "Values": ["{{parentSpanId}}"]
    },
    {
      "Key": "X-B3-Sampled",
      "Values": ["1"]
    }
  ]'
```

## Monitoring and Observability

### Log Correlation

Ensure all log entries can be correlated:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Log-Correlation-ID",
      "Values": ["{{correlationId}}"]
    },
    {
      "Key": "X-Request-Start-Time",
      "Values": ["{{timestamp}}"]
    },
    {
      "Key": "X-Source-Service",
      "Values": ["test-client"]
    },
    {
      "Key": "X-Target-Service",
      "Values": ["api-server"]
    }
  ]'
```

### Performance Monitoring Headers

Add headers for performance tracking:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Request-Received-At",
      "Values": ["{{timestamp}}"]
    },
    {
      "Key": "X-Proxy-Processing-Start",
      "Values": ["{{processingStartTime}}"]
    },
    {
      "Key": "X-Expected-Max-Duration",
      "Values": ["5000"]
    }
  ]'
```

### Security Context Headers

Add security-related metadata:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/decorate \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Key": "X-Security-Context",
      "Values": ["test-environment"]
    },
    {
      "Key": "X-Authentication-Type",
      "Values": ["bearer-token"]
    },
    {
      "Key": "X-Authorization-Level",
      "Values": ["read-write"]
    }
  ]'
```

## Integration Examples

### Kubernetes Service Mesh Integration

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: test-client
spec:
  template:
    spec:
      containers:
      - name: test-client
        env:
        - name: HTTP_PROXY
          value: "http://slipka-proxy.default.svc.cluster.local:8080"
        - name: PROXY_DECORATION
          value: |
            [
              {
                "Key": "X-Kubernetes-Service",
                "Values": ["test-client"]
              },
              {
                "Key": "X-Kubernetes-Namespace",
                "Values": ["default"]
              },
              {
                "Key": "X-Kubernetes-Pod",
                "Values": ["$(POD_NAME)"]
              }
            ]
```

### Microservice Architecture Tracing

```javascript
// tracing-helper.js
const axios = require('axios');

class TracedClient {
  constructor(proxyUrl, serviceName) {
    this.proxyUrl = proxyUrl;
    this.serviceName = serviceName;
    this.setupDecoration();
  }

  async setupDecoration() {
    const decorations = [
      {
        Key: "X-Service-Name",
        Values: [this.serviceName]
      },
      {
        Key: "X-Trace-ID",
        Values: [this.generateTraceId()]
      },
      {
        Key: "X-Parent-Service",
        Values: [process.env.SERVICE_NAME || 'unknown']
      }
    ];

    await axios.put(`${this.proxyUrl}/decorate`, decorations);
  }

  generateTraceId() {
    return Math.random().toString(36).substring(2, 15);
  }

  async get(endpoint) {
    return axios.get(`${this.proxyUrl}${endpoint}`);
  }

  async post(endpoint, data) {
    return axios.post(`${this.proxyUrl}${endpoint}`, data);
  }
}

// Usage
const client = new TracedClient('http://localhost:8080/api/Proxies/proxy_123', 'order-service');
const response = await client.get('/api/orders/123');
```

### CI/CD Pipeline Integration

```yaml
# .github/workflows/test.yml
name: Integration Tests
on: [push]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - name: Create Decorated Proxy
        run: |
          PROXY_ID=$(curl -X POST http://localhost:8080/api/Proxies \
            -H "Content-Type: application/json" \
            -d "{
              \"Name\": \"ci-test-proxy\",
              \"TargetHost\": \"api.test.com\",
              \"TargetPort\": 443,
              \"TargetPortHttps\": true
            }" | jq -r '.id')

          # Add CI/CD decorations
          curl -X PUT http://localhost:8080/api/Proxies/$PROXY_ID/decorate \
            -H "Content-Type: application/json" \
            -d "[
              {
                \"Key\": \"X-CI-Pipeline\",
                \"Values\": [\"${{ github.workflow }}\"]
              },
              {
                \"Key\": \"X-CI-Run-ID\",
                \"Values\": [\"${{ github.run_id }}\"]
              },
              {
                \"Key\": \"X-Git-Commit\",
                \"Values\": [\"${{ github.sha }}\"]
              },
              {
                \"Key\": \"X-Git-Branch\",
                \"Values\": [\"${{ github.ref_name }}\"]
              }
            ]"

          echo "PROXY_ID=$PROXY_ID" >> $GITHUB_ENV
          echo "PROXY_PORT=$(curl http://localhost:8080/api/Proxies/$PROXY_ID | jq -r '.port')" >> $GITHUB_ENV

      - name: Run Tests
        run: |
          # Run tests using decorated proxy
          npm test -- --proxy-port=$PROXY_PORT

      - name: Analyze Decorated Traffic
        run: |
          # Get all traffic with CI decorations
          curl -X GET "http://localhost:8080/api/Sessions/$(curl http://localhost:8080/api/Proxies/$PROXY_ID | jq -r '.sessionId')/calls" \
            > ci-traffic.json

      - name: Cleanup
        if: always()
        run: |
          curl -X DELETE http://localhost:8080/api/Proxies/$PROXY_ID
```

### Log Aggregation Setup

```javascript
// elk-integration.js
const winston = require('winston');
const Elasticsearch = require('winston-elasticsearch');

const esTransportOpts = {
  level: 'info',
  indexPrefix: 'slipka-proxy-logs',
  clientOpts: { node: 'http://localhost:9200' }
};

const logger = winston.createLogger({
  transports: [
    new Elasticsearch(esTransportOpts)
  ]
});

// Middleware to extract decoration headers for logging
function logWithDecorations(req, res, next) {
  const correlationId = req.headers['x-correlation-id'];
  const serviceName = req.headers['x-service-name'];

  logger.info('Request processed', {
    correlationId,
    serviceName,
    method: req.method,
    url: req.url,
    statusCode: res.statusCode,
    duration: Date.now() - req.startTime
  });

  next();
}

// Apply to proxy responses
app.use('/api/*', logWithDecorations);
```

### Monitoring Dashboard

```javascript
// monitoring-dashboard.js
const express = require('express');
const app = express();

app.get('/api/traffic-analysis', async (req, res) => {
  try {
    // Get all sessions with decorated traffic
    const sessions = await fetch('http://localhost:8080/api/Sessions');
    const sessionData = await sessions.json();

    const analysis = {
      totalRequests: 0,
      services: new Map(),
      environments: new Map(),
      correlations: new Map()
    };

    for (const session of sessionData) {
      const calls = await fetch(`http://localhost:8080/api/Sessions/${session.id}/calls`);
      const callData = await calls.json();

      analysis.totalRequests += callData.length;

      callData.forEach(call => {
        // Analyze service decorations
        const serviceName = call.request.headers.find(h => h.key === 'X-Service-Name')?.values[0];
        if (serviceName) {
          analysis.services.set(serviceName, (analysis.services.get(serviceName) || 0) + 1);
        }

        // Analyze environment decorations
        const environment = call.request.headers.find(h => h.key === 'X-Environment')?.values[0];
        if (environment) {
          analysis.environments.set(environment, (analysis.environments.get(environment) || 0) + 1);
        }

        // Analyze correlation patterns
        const correlationId = call.request.headers.find(h => h.key === 'X-Correlation-ID')?.values[0];
        if (correlationId) {
          const requests = analysis.correlations.get(correlationId) || [];
          requests.push(call);
          analysis.correlations.set(correlationId, requests);
        }
      });
    }

    res.json({
      totalRequests: analysis.totalRequests,
      services: Object.fromEntries(analysis.services),
      environments: Object.fromEntries(analysis.environments),
      correlationCount: analysis.correlations.size,
      averageRequestsPerCorrelation: analysis.totalRequests / analysis.correlations.size
    });

  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

app.listen(3000, () => console.log('Monitoring dashboard running on port 3000'));
```

## Best Practices

### 1. Header Naming Conventions
- Use `X-` prefix for custom headers (RFC 7230)
- Use consistent naming across services
- Document header meanings and expected values

### 2. Correlation ID Strategy
- Generate unique IDs for each request chain
- Propagate correlation IDs across service boundaries
- Include correlation IDs in all log entries

### 3. Performance Considerations
- Minimize the number of decoration headers
- Use efficient header generation
- Monitor proxy performance impact

### 4. Security Considerations
- Don't expose sensitive information in decoration headers
- Validate header content to prevent injection attacks
- Use HTTPS when transmitting decorated traffic

## Troubleshooting

### Common Issues

1. **Headers Not Appearing**
   - Verify decoration rules are active
   - Check proxy configuration and status
   - Ensure requests are going through the proxy

2. **Dynamic Values Not Working**
   - Check placeholder syntax (e.g., `{{correlationId}}`)
   - Verify proxy has access to required context
   - Review error logs for placeholder resolution issues

3. **Header Conflicts**
   - Some headers may be overwritten by target services
   - Use unique header names to avoid conflicts
   - Check target service behavior with custom headers

### Debugging

Enable debug logging for decoration issues:

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Slipka.Proxy": "Debug"
      }
    }
  }
}
```

Check active decoration rules:

```bash
curl -X GET http://localhost:8080/api/Proxies/proxy_12345
```

Verify headers are being added:

```bash
# Make a request through the proxy
curl -v -X GET "http://localhost:61710/api/test" \
  -H "X-Test-Header: test-value"
```

## Advanced Configuration

### Custom Header Generators

For complex header generation logic, consider extending Slipka with custom preprocessors that generate decoration headers dynamically based on request content, user context, or external data sources.

### Header Validation

Add validation for decoration headers:

```json
{
  "DecorationValidation": {
    "RequiredHeaders": ["X-Correlation-ID", "X-Source"],
    "HeaderPatterns": {
      "X-Correlation-ID": "^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$",
      "X-Source": "^(api|web|mobile)$"
    }
  }
}
```

### Conditional Decoration

While basic decoration applies to all requests, advanced setups can use routing rules to apply different decorations based on request characteristics.

The Decorating feature is essential for maintaining observability and traceability in complex distributed systems, ensuring that every request can be tracked from origin to destination across multiple services and infrastructure components.
