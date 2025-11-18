# Recording

The Recording feature captures and stores HTTP traffic passing through Slipka proxies, enabling detailed analysis, replay, and verification of API interactions. This is particularly valuable for testing scenarios involving file downloads, complex request/response cycles, and behavioral verification.

## Overview

Recording intercepts and stores both requests and responses, including headers, body content, and metadata. Stored traffic can be retrieved later for analysis, testing, or compliance purposes.

## Key Features

- **Complete Traffic Capture**: Records full request/response cycles
- **Selective Recording**: Filter traffic by method, URI patterns, and headers
- **File Storage**: Automatic storage of large payloads using GridFS
- **Metadata Enrichment**: Adds timing, status, and correlation information
- **Session Association**: Links recordings to specific test sessions
- **Retrieval API**: Access recorded traffic through REST endpoints

## API Endpoints

### Configure Recording
**Endpoint:** `PUT /api/Proxies/{id}/record`

Adds recording rules to an existing proxy.

### Remove Recording
**Endpoint:** `PUT /api/Proxies/{id}/record`

Pass an empty array to remove all recording rules.

### Retrieve Recorded Traffic
**Endpoint:** `GET /api/Sessions/{sessionId}/calls`

Returns all recorded calls for a session.

**Endpoint:** `GET /api/Sessions/{sessionId}/calls/{callNumber}`

Returns a specific recorded call.

## Configuration

### Recording Message Structure

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
    "Content": "optional response body pattern",
    "Headers": [
      {
        "Key": "Header-Name",
        "Values": ["expected-value"]
      }
    ]
  },
  "StatusCode": "200",
  "Method": "GET",
  "Uri": "/api/data/*",
  "Duration": 1000
}
```

### Configuration Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Request` | MessageTemplate | No | Pattern to match in request content/headers |
| `Response` | MessageTemplate | No | Pattern to match in response content/headers |
| `StatusCode` | string | No | HTTP status code to match |
| `Method` | string | No | HTTP method to match |
| `Uri` | string | No | URI pattern to match (supports wildcards) |
| `Duration` | int | No | Expected duration range (for filtering) |

## Usage Examples

### Basic Traffic Recording

Record all API calls to analyze usage patterns:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/*"
    }
  ]'
```

### Selective Recording by Method

Record only write operations for audit purposes:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/*"
    },
    {
      "Method": "PUT",
      "Uri": "/api/*"
    },
    {
      "Method": "DELETE",
      "Uri": "/api/*"
    }
  ]'
```

### Recording File Downloads

Capture file downloads for Selenium testing:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/files/*",
      "Response": {
        "Headers": [
          {"Key": "Content-Type", "Values": ["application/pdf", "application/octet-stream", "image/*"]}
        ]
      }
    }
  ]'
```

### Recording with Response Filtering

Record only error responses for debugging:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "StatusCode": "4*",
      "Uri": "/api/*"
    },
    {
      "StatusCode": "5*",
      "Uri": "/api/*"
    }
  ]'
```

### Recording Slow Requests

Capture requests that take longer than expected:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/*",
      "Duration": 5000
    }
  ]'
```

## Retrieving Recorded Traffic

### Get All Recorded Calls

```bash
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls
```

**Response:**
```json
[
  {
    "callNumber": 1,
    "method": "GET",
    "uri": "/api/users/123",
    "statusCode": 200,
    "duration": 245,
    "timestamp": "2025-11-18T14:30:15Z",
    "request": {
      "headers": [
        {"key": "User-Agent", "values": ["curl/7.68.0"]},
        {"key": "Accept", "values": ["application/json"]}
      ],
      "contentLength": 0
    },
    "response": {
      "headers": [
        {"key": "Content-Type", "values": ["application/json"]},
        {"key": "Content-Length", "values": ["128"]}
      ],
      "contentLength": 128
    }
  }
]
```

### Get Specific Call Details

```bash
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls/1
```

**Response:**
```json
{
  "callNumber": 1,
  "method": "GET",
  "uri": "/api/users/123",
  "statusCode": 200,
  "duration": 245,
  "timestamp": "2025-11-18T14:30:15Z",
  "request": {
    "headers": [
      {"key": "User-Agent", "values": ["curl/7.68.0"]},
      {"key": "Accept", "values": ["application/json"]}
    ],
    "contentId": "file_507f1f77bcf86cd799439011"
  },
  "response": {
    "headers": [
      {"key": "Content-Type", "values": ["application/json"]},
      {"key": "Content-Length", "values": ["128"]}
    ],
    "contentId": "file_507f1f77bcf86cd799439012"
  }
}
```

### Download Recorded Content

```bash
# Download request body
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls/1/request

# Download response body
curl -X GET http://localhost:8080/api/Sessions/session_abc123/calls/1/response
```

## Static Proxy Recording Configuration

Recording can be configured in static proxy definitions:

```json
{
  "StaticProxies": {
    "Proxies": [
      {
        "Id": "production-monitor",
        "Port": 61800,
        "TargetHost": "api.production.com",
        "TargetPort": 443,
        "Name": "Production Traffic Monitor",
        "AutoStart": true,
        "RecordedCalls": [
          {
            "Method": "POST",
            "Uri": "/api/orders",
            "Tags": ["production-order"]
          },
          {
            "Method": "GET",
            "Uri": "/api/users/*/profile",
            "Tags": ["user-data-access"]
          }
        ]
      }
    ]
  }
}
```

## Advanced Recording Scenarios

### Multi-Part Form Data Recording

Record file uploads and form submissions:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/upload",
      "Request": {
        "Headers": [
          {"Key": "Content-Type", "Values": ["multipart/form-data*"]}
        ]
      }
    }
  ]'
```

### API Traffic Analysis

Record all API traffic for usage analytics:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/v*/**",
      "Tags": ["api-traffic"]
    }
  ]'
```

### Performance Monitoring

Record slow API calls for optimization:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/reports/*",
      "Duration": 3000,
      "Tags": ["slow-query", "performance-issue"]
    }
  ]'
```

### Security Audit Logging

Record sensitive operations for compliance:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "POST",
      "Uri": "/api/admin/*",
      "Tags": ["admin-action", "audit-required"]
    },
    {
      "Method": "DELETE",
      "Uri": "/api/users/*",
      "Tags": ["user-deletion", "audit-required"]
    }
  ]'
```

## Pattern Matching

### URI Patterns

Support for flexible URI matching:

- **Exact Match**: `/api/users/123`
- **Single Wildcard**: `/api/users/*` (matches any single segment)
- **Multi-Wildcard**: `/api/**` (matches any number of segments)
- **Mixed Patterns**: `/api/v*/users/*/profile`

### Header Matching

Match requests based on header values:

```json
{
  "Request": {
    "Headers": [
      {
        "Key": "Authorization",
        "Values": ["Bearer *"]
      },
      {
        "Key": "X-API-Key",
        "Values": ["*"]
      }
    ]
  }
}
```

### Content Pattern Matching

Match request/response bodies using wildcards:

```json
{
  "Request": {
    "Content": "*\"action\": \"delete\"*"
  },
  "Response": {
    "Content": "*\"error\": *"
  }
}
```

## Data Storage and Retrieval

### File Storage (GridFS)

Large request/response bodies are automatically stored using MongoDB GridFS:

- **Automatic Detection**: Bodies larger than 16MB are stored in GridFS
- **Content-Type Preservation**: Original content types are maintained
- **Efficient Retrieval**: Streamed access to large files
- **Cleanup Management**: Automatic cleanup based on retention policies

### Metadata Storage

All recordings include comprehensive metadata:

```json
{
  "callNumber": 1,
  "sessionId": "session_abc123",
  "timestamp": "2025-11-18T14:30:15.123Z",
  "duration": 245,
  "method": "POST",
  "uri": "/api/users",
  "statusCode": 201,
  "requestHeadersCount": 5,
  "requestContentLength": 128,
  "responseHeadersCount": 8,
  "responseContentLength": 256,
  "tags": ["user-creation", "api-v2"],
  "correlationId": "corr_12345"
}
```

## Integration Examples

### Selenium Testing with File Downloads

```javascript
const { Builder, By, until } = require('selenium-webdriver');
const chrome = require('selenium-webdriver/chrome');
const fs = require('fs');

async function testFileDownload() {
  // Configure proxy for recording
  const proxyResponse = await fetch('http://localhost:8080/api/Proxies', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      Name: 'selenium-download-test',
      TargetHost: 'example.com',
      TargetPort: 443,
      TargetPortHttps: true,
      RecordedCalls: [{
        Method: 'GET',
        Uri: '/files/*',
        Response: {
          Headers: [
            { Key: 'Content-Type', Values: ['application/pdf'] }
          ]
        }
      }]
    })
  });

  const proxy = await proxyResponse.json();

  // Configure Chrome with proxy
  const chromeOptions = new chrome.Options();
  chromeOptions.addArguments(`--proxy-server=http://localhost:${proxy.port}`);

  const driver = new Builder()
    .forBrowser('chrome')
    .setChromeOptions(chromeOptions)
    .build();

  try {
    // Navigate and trigger download
    await driver.get('https://example.com/download-page');
    await driver.findElement(By.id('download-btn')).click();

    // Wait for download to complete
    await driver.wait(until.titleContains('Download Complete'), 10000);

    // Retrieve recorded traffic
    const recordings = await fetch(`http://localhost:8080/api/Sessions/${proxy.sessionId}/calls`);
    const calls = await recordings.json();

    // Find and verify the download
    const downloadCall = calls.find(call =>
      call.uri.includes('/files/') && call.method === 'GET'
    );

    if (downloadCall) {
      // Download the file content
      const fileResponse = await fetch(`http://localhost:8080/api/Sessions/${proxy.sessionId}/calls/${downloadCall.callNumber}/response`);
      const fileBlob = await fileResponse.blob();

      // Save file locally
      fs.writeFileSync('downloaded-file.pdf', Buffer.from(await fileBlob.arrayBuffer()));
      console.log('File downloaded and saved successfully');
    }

  } finally {
    await driver.quit();

    // Cleanup proxy
    await fetch(`http://localhost:8080/api/Proxies/${proxy.id}`, {
      method: 'DELETE'
    });
  }
}
```

### API Contract Testing

```bash
# Record actual API responses for contract verification
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "contract-test-recorder",
    "TargetHost": "api.contract-test.com",
    "TargetPort": 443,
    "TargetPortHttps": true,
    "RecordedCalls": [
      {
        "Method": "GET",
        "Uri": "/api/users/*"
      },
      {
        "Method": "POST",
        "Uri": "/api/users"
      }
    ]
  }'

# Make test calls
curl -X GET "http://localhost:61710/api/users/123"
curl -X POST "http://localhost:61710/api/users" \
  -H "Content-Type: application/json" \
  -d '{"name": "Test User"}'

# Retrieve and verify recordings
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls"
```

### Load Testing Analysis

```bash
# Configure recording for load test analysis
curl -X PUT http://localhost:8080/api/Proxies/load-test-proxy/record \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/stress-test",
      "Tags": ["load-test", "performance-analysis"]
    }
  ]'

# Run load test (using tools like k6, JMeter, etc.)
# k6 run load-test.js

# Analyze recorded traffic
curl -X GET "http://localhost:8080/api/Sessions/session_load123/calls" | \
  jq '.[] | select(.duration > 5000) | {uri, duration, statusCode}'
```

### Compliance and Audit Logging

```bash
# Configure comprehensive audit recording
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "compliance-monitor",
    "TargetHost": "api.compliant-app.com",
    "TargetPort": 443,
    "TargetPortHttps": true,
    "RecordedCalls": [
      {
        "Method": "POST",
        "Uri": "/api/pii/*",
        "Tags": ["pii-access", "gdpr-audit"]
      },
      {
        "Method": "GET",
        "Uri": "/api/sensitive-data/*",
        "Tags": ["sensitive-data", "audit-required"]
      },
      {
        "Method": "DELETE",
        "Uri": "/api/accounts/*",
        "Tags": ["account-deletion", "regulatory-audit"]
      }
    ]
  }'

# Export audit logs
curl -X GET "http://localhost:8080/api/Sessions/session_compliance/calls" \
  -H "Accept: application/json" > audit-log-$(date +%Y%m%d).json
```

## Best Practices

### 1. Storage Management
- Configure appropriate retention periods
- Monitor disk usage with large recordings
- Use selective recording to reduce storage overhead

### 2. Performance Considerations
- Be specific with recording patterns to minimize overhead
- Monitor proxy performance impact during recording
- Use sampling for high-traffic scenarios

### 3. Privacy and Compliance
- Avoid recording sensitive data unnecessarily
- Implement data masking for PII in recordings
- Follow data retention policies for recorded traffic

### 4. Analysis and Monitoring
- Regularly review recorded traffic patterns
- Set up alerts for unusual traffic patterns
- Use recordings for capacity planning

## Troubleshooting

### Common Issues

1. **Missing Recordings**
   - Verify recording rules are active
   - Check pattern matching syntax
   - Ensure proxy is receiving traffic

2. **Large File Handling**
   - GridFS should handle files automatically
   - Check MongoDB connectivity for file storage
   - Monitor available disk space

3. **Performance Impact**
   - Reduce recording scope
   - Use more specific patterns
   - Consider sampling strategies

### Debugging

Enable debug logging for recording issues:

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Slipka.Repositories": "Debug",
        "Slipka.Proxy": "Debug"
      }
    }
  }
}
```

Check recording status:

```bash
curl -X GET http://localhost:8080/api/Proxies/proxy_12345
```

## Data Export and Analysis

### Export Recordings

```bash
# Export all recordings as JSON
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" \
  -H "Accept: application/json" > recordings.json

# Export specific recording details
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls/1" \
  > recording-details.json
```

### Performance Analysis

```bash
# Analyze response times
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '.[] | select(.method == "GET") | .duration' | \
  awk '{sum+=$1; count++} END {print "Average:", sum/count, "ms"}'

# Find error responses
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '.[] | select(.statusCode >= 400) | {uri, statusCode, duration}'
```

### Traffic Pattern Analysis

```bash
# Group by endpoint and method
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq 'group_by(.uri, .method) | map({endpoint: .[0].uri, method: .[0].method, count: length})'

# Analyze peak traffic times
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '.[] | {timestamp: .timestamp, hour: (.timestamp | strptime("%Y-%m-%dT%H:%M:%SZ") | strftime("%H"))} | group_by(.hour)'
```

The Recording feature provides comprehensive traffic capture capabilities essential for testing, debugging, compliance, and performance analysis in complex distributed systems.
