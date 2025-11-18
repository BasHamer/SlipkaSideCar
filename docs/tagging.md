# Tagging

The Tagging feature enables you to add custom metadata labels to HTTP traffic, providing powerful categorization and reporting capabilities. Tags can be used to classify traffic by test scenarios, performance characteristics, business logic, or any other criteria important for analysis.

## Overview

Tags are key-value metadata attached to request/response pairs that help organize, filter, and analyze proxy traffic. They enable sophisticated reporting, alerting, and analytics across different testing scenarios and production monitoring use cases.

## Key Features

- **Flexible Categorization**: Add multiple tags to classify traffic by any criteria
- **Pattern-Based Tagging**: Automatically tag traffic based on request/response patterns
- **Reporting and Analytics**: Use tags for filtering and aggregating traffic data
- **Performance Monitoring**: Tag slow requests, errors, or high-volume endpoints
- **Business Logic Classification**: Tag by user types, API versions, or business domains
- **Integration Ready**: Tags are included in all traffic data exports and APIs

## API Endpoints

### Configure Tagging
**Endpoint:** `PUT /api/Proxies/{id}/tag`

Adds tagging rules to an existing proxy.

### Remove Tagging
**Endpoint:** `PUT /api/Proxies/{id}/tag`

Pass an empty array to remove all tagging rules.

### Query Tagged Traffic
**Endpoint:** `GET /api/Sessions/{sessionId}/calls?tags=tag1,tag2`

Filter recorded calls by tags.

## Configuration

### Tagging Message Structure

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
  "Uri": "/api/users/*",
  "Duration": 1000,
  "Tags": ["user-operations", "read-heavy", "api-v2"]
}
```

### Configuration Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Request` | MessageTemplate | No | Pattern to match in request content/headers |
| `Response` | MessageTemplate | No | Pattern to match in response content/headers |
| `StatusCode` | string | No | HTTP status code pattern to match |
| `Method` | string | No | HTTP method to match |
| `Uri` | string | No | URI pattern to match (supports wildcards) |
| `Duration` | int | No | Duration threshold for tagging |
| `Tags` | string[] | Yes | Array of tag strings to apply |

## Usage Examples

### Basic API Operation Tagging

Categorize API operations by functionality:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/users/*",
      "Tags": ["user-management", "read-operation"]
    },
    {
      "Method": "POST",
      "Uri": "/api/users",
      "Tags": ["user-management", "create-operation"]
    },
    {
      "Method": "PUT",
      "Uri": "/api/users/*",
      "Tags": ["user-management", "update-operation"]
    },
    {
      "Method": "DELETE",
      "Uri": "/api/users/*",
      "Tags": ["user-management", "delete-operation"]
    }
  ]'
```

### Performance-Based Tagging

Tag slow requests for performance analysis:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/*",
      "Duration": 5000,
      "Tags": ["slow-request", "performance-issue"]
    },
    {
      "Method": "*",
      "Uri": "/api/*",
      "Duration": 10000,
      "Tags": ["very-slow-request", "critical-performance"]
    }
  ]'
```

### Error Response Tagging

Categorize different types of error responses:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "StatusCode": "4*",
      "Uri": "/api/*",
      "Tags": ["client-error"]
    },
    {
      "StatusCode": "5*",
      "Uri": "/api/*",
      "Tags": ["server-error"]
    },
    {
      "StatusCode": "401",
      "Uri": "/api/*",
      "Tags": ["authentication-error", "security-issue"]
    },
    {
      "StatusCode": "403",
      "Uri": "/api/*",
      "Tags": ["authorization-error", "security-issue"]
    }
  ]'
```

### Business Domain Tagging

Tag traffic by business domains or microservices:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/api/orders/*",
      "Tags": ["ecommerce", "orders-service"]
    },
    {
      "Method": "*",
      "Uri": "/api/inventory/*",
      "Tags": ["ecommerce", "inventory-service"]
    },
    {
      "Method": "*",
      "Uri": "/api/payments/*",
      "Tags": ["ecommerce", "payments-service", "pci-compliant"]
    },
    {
      "Method": "*",
      "Uri": "/api/users/*",
      "Tags": ["user-management", "identity-service", "gdpr-sensitive"]
    }
  ]'
```

### User Type and Plan Tagging

Differentiate traffic based on user characteristics:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Request": {
        "Headers": [
          {"Key": "X-User-Plan", "Values": ["free"]}
        ]
      },
      "Tags": ["free-tier", "rate-limited"]
    },
    {
      "Request": {
        "Headers": [
          {"Key": "X-User-Plan", "Values": ["premium"]}
        ]
      },
      "Tags": ["premium-tier", "high-priority"]
    },
    {
      "Request": {
        "Headers": [
          {"Key": "X-User-Type", "Values": ["admin"]}
        ]
      },
      "Tags": ["admin-user", "elevated-permissions"]
    }
  ]'
```

### API Version Tagging

Track usage across different API versions:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "*",
      "Uri": "/v1/*",
      "Tags": ["api-v1", "legacy-api", "deprecated"]
    },
    {
      "Method": "*",
      "Uri": "/v2/*",
      "Tags": ["api-v2", "current-api"]
    },
    {
      "Method": "*",
      "Uri": "/v3/*",
      "Tags": ["api-v3", "beta-api", "experimental"]
    }
  ]'
```

## Querying Tagged Traffic

### Filter by Single Tag

```bash
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=performance-issue"
```

### Filter by Multiple Tags (AND logic)

```bash
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=slow-request,api-v2"
```

### Filter by Multiple Tags (OR logic)

```bash
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=error&operator=or&tags=server-error"
```

## Static Proxy Tagging Configuration

Tags can be configured in static proxy definitions:

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
        "TaggedCalls": [
          {
            "Method": "POST",
            "Uri": "/api/orders",
            "Tags": ["production-order", "revenue-generating"]
          },
          {
            "Method": "GET",
            "Uri": "/api/users/*/profile",
            "Tags": ["user-data-access", "gdpr-sensitive"]
          },
          {
            "StatusCode": "5*",
            "Tags": ["production-incident", "alert-required"]
          }
        ]
      }
    ]
  }
}
```

## Advanced Tagging Scenarios

### Conditional Tagging Based on Response Content

Tag responses based on their content:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/search",
      "Response": {
        "Content": "*\"results\": []*"
      },
      "Tags": ["empty-search-results", "ux-improvement"]
    },
    {
      "Method": "POST",
      "Uri": "/api/payments",
      "Response": {
        "Content": "*\"status\": \"declined\"*"
      },
      "Tags": ["payment-declined", "business-impact"]
    }
  ]'
```

### Geographic and Regional Tagging

Tag traffic based on geographic routing:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Request": {
        "Headers": [
          {"Key": "CF-IPCountry", "Values": ["US"]}
        ]
      },
      "Tags": ["us-traffic", "north-america"]
    },
    {
      "Request": {
        "Headers": [
          {"Key": "CF-IPCountry", "Values": ["GB", "DE", "FR"]}
        ]
      },
      "Tags": ["eu-traffic", "gdpr-region"]
    },
    {
      "Request": {
        "Headers": [
          {"Key": "CF-IPCountry", "Values": ["JP", "KR", "CN"]}
        ]
      },
      "Tags": ["apac-traffic", "asia-pacific"]
    }
  ]'
```

### Load Balancing and Infrastructure Tagging

Tag traffic by infrastructure components:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Response": {
        "Headers": [
          {"Key": "X-Server", "Values": ["web-01", "web-02"]}
        ]
      },
      "Tags": ["web-tier", "load-balanced"]
    },
    {
      "Response": {
        "Headers": [
          {"Key": "X-Cache", "Values": ["HIT"]}
        ]
      },
      "Tags": ["cache-hit", "optimized"]
    },
    {
      "Response": {
        "Headers": [
          {"Key": "X-Cache", "Values": ["MISS"]}
        ]
      },
      "Tags": ["cache-miss", "optimization-opportunity"]
    }
  ]'
```

### Security Event Tagging

Tag security-related events:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "StatusCode": "401",
      "Uri": "/api/*",
      "Tags": ["authentication-failure", "security-event"]
    },
    {
      "StatusCode": "403",
      "Uri": "/api/admin/*",
      "Tags": ["authorization-failure", "security-event", "admin-access"]
    },
    {
      "Method": "POST",
      "Uri": "/api/login",
      "Request": {
        "Content": "*\"attempts\"*"
      },
      "Tags": ["brute-force-attempt", "security-threat"]
    }
  ]'
```

## Pattern Matching

### Advanced URI Patterns

Complex pattern matching for sophisticated tagging:

```json
{
  "Uri": "/api/v[0-9]+/users/[0-9]+/posts/[0-9]+",
  "Tags": ["nested-resource", "complex-uri"]
}
```

### Header Pattern Matching

Match headers with wildcards and patterns:

```json
{
  "Request": {
    "Headers": [
      {
        "Key": "User-Agent",
        "Values": ["*Chrome/*", "*Firefox/*"]
      },
      {
        "Key": "X-Request-ID",
        "Values": ["req_*"]
      }
    ]
  },
  "Tags": ["browser-request", "tracked-request"]
}
```

### Content Pattern Matching

Match request/response bodies:

```json
{
  "Request": {
    "Content": "*\"payment_method\": \"credit_card\"*"
  },
  "Response": {
    "Content": "*\"transaction_id\": \"*\"*"
  },
  "Tags": ["credit-card-payment", "successful-transaction"]
}
```

## Analytics and Reporting

### Tag-Based Traffic Analysis

```bash
# Count requests by tag
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '[.[] | .tags[]] | group_by(.) | map({tag: .[0], count: length})'

# Find most active endpoints by tag
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq 'group_by(.tags[]) | map({tag: .[0].tags[0], calls: length}) | sort_by(.calls) | reverse'

# Analyze error rates by API version
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=api-v1" | \
  jq '. | map(select(.statusCode >= 400)) | length / length * 100'
```

### Performance Analysis by Tags

```bash
# Average response time by tag
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq 'group_by(.tags[]) | map({tag: .[0].tags[0], avg_duration: (map(.duration) | add / length)})'

# Find slowest requests by category
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=performance-issue" | \
  jq 'sort_by(.duration) | reverse | .[0:10] | map({uri, duration, tags})'
```

### Business Metrics

```bash
# Revenue-impacting requests
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=revenue-generating" | \
  jq 'length'

# GDPR-sensitive data access
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls?tags=gdpr-sensitive" | \
  jq 'group_by(.uri) | map({endpoint: .[0].uri, access_count: length})'
```

## Integration Examples

### CI/CD Pipeline Integration

```yaml
# .github/workflows/performance-test.yml
name: Performance Test Analysis
on: [push]

jobs:
  performance-test:
    runs-on: ubuntu-latest
    steps:
      - name: Create Tagged Proxy
        run: |
          PROXY_RESPONSE=$(curl -X POST http://localhost:8080/api/Proxies \
            -H "Content-Type: application/json" \
            -d '{
              "Name": "performance-test-proxy",
              "TargetHost": "api.test.com",
              "TargetPort": 443,
              "TargetPortHttps": true,
              "TaggedCalls": [
                {
                  "Method": "*",
                  "Uri": "/api/*",
                  "Duration": 2000,
                  "Tags": ["slow-endpoint", "performance-test"]
                },
                {
                  "StatusCode": "5*",
                  "Tags": ["server-error", "performance-test"]
                }
              ]
            }')
          echo "PROXY_ID=$(echo $PROXY_RESPONSE | jq -r '.id')" >> $GITHUB_ENV

      - name: Run Performance Tests
        run: |
          # Run k6, JMeter, or other load testing tools
          # Tests will go through the tagged proxy

      - name: Analyze Results
        run: |
          # Get tagged slow requests
          curl -X GET "http://localhost:8080/api/Sessions/session_123/calls?tags=slow-endpoint" \
            -H "Accept: application/json" > slow-requests.json

          # Count server errors
          ERROR_COUNT=$(curl -X GET "http://localhost:8080/api/Sessions/session_123/calls?tags=server-error" | jq length)

          if [ "$ERROR_COUNT" -gt 10 ]; then
            echo "High error rate detected: $ERROR_COUNT errors"
            exit 1
          fi

      - name: Cleanup
        if: always()
        run: |
          curl -X DELETE http://localhost:8080/api/Proxies/$PROXY_ID
```

### Monitoring Dashboard Integration

```javascript
// dashboard.js - Real-time monitoring dashboard
async function updateDashboard() {
  const sessionId = 'current-session-id';

  // Get error metrics
  const errors = await fetch(`/api/Sessions/${sessionId}/calls?tags=server-error`);
  const errorData = await errors.json();

  // Get performance metrics
  const slowRequests = await fetch(`/api/Sessions/${sessionId}/calls?tags=performance-issue`);
  const perfData = await slowRequests.json();

  // Get business metrics
  const revenueCalls = await fetch(`/api/Sessions/${sessionId}/calls?tags=revenue-generating`);
  const businessData = await revenueCalls.json();

  // Update dashboard
  updateChart('errors', errorData.length);
  updateChart('slow-requests', perfData.length);
  updateChart('revenue-impacting', businessData.length);

  // Alert on thresholds
  if (errorData.length > 100) {
    alert('High error rate detected!');
  }
}

setInterval(updateDashboard, 30000); // Update every 30 seconds
```

### Log Aggregation Integration

```javascript
// elk-integration.js - Send tagged traffic to ELK stack
const { Client } = require('@elastic/elasticsearch');

const esClient = new Client({ node: 'http://localhost:9200' });

async function exportTaggedTraffic(sessionId, tags) {
  const response = await fetch(`/api/Sessions/${sessionId}/calls?tags=${tags.join(',')}`);
  const calls = await response.json();

  const bulkBody = calls.flatMap(call => [
    { index: { _index: 'proxy-traffic', _id: `${sessionId}_${call.callNumber}` } },
    {
      session_id: sessionId,
      call_number: call.callNumber,
      timestamp: call.timestamp,
      method: call.method,
      uri: call.uri,
      status_code: call.statusCode,
      duration: call.duration,
      tags: call.tags,
      // Include other relevant fields
    }
  ]);

  await esClient.bulk({ body: bulkBody });
}

// Export different tag categories
await exportTaggedTraffic('session_123', ['security-event']);
await exportTaggedTraffic('session_123', ['performance-issue']);
await exportTaggedTraffic('session_123', ['gdpr-sensitive']);
```

### Alerting System Integration

```python
# alerting.py - Automated alerting based on tags
import requests
import time

def check_alerts(session_id):
    # Check for critical errors
    response = requests.get(f"http://localhost:8080/api/Sessions/{session_id}/calls?tags=security-event")
    security_events = response.json()

    if len(security_events) > 5:
        send_alert("Security Alert", f"{len(security_events)} security events detected")

    # Check for performance degradation
    response = requests.get(f"http://localhost:8080/api/Sessions/{session_id}/calls?tags=performance-issue")
    perf_issues = response.json()

    if len(perf_issues) > 20:
        send_alert("Performance Alert", f"{len(perf_issues)} slow requests detected")

    # Check for business impact
    response = requests.get(f"http://localhost:8080/api/Sessions/{session_id}/calls?tags=revenue-generating")
    revenue_calls = response.json()

    error_rate = len([c for c in revenue_calls if c['statusCode'] >= 500]) / len(revenue_calls)

    if error_rate > 0.05:  # 5% error rate
        send_alert("Business Impact Alert", f"Revenue-generating endpoints have {error_rate*100:.1f}% error rate")

def send_alert(title, message):
    # Integrate with PagerDuty, Slack, email, etc.
    print(f"ALERT: {title} - {message}")

# Run continuous monitoring
while True:
    check_alerts("current-session-id")
    time.sleep(60)  # Check every minute
```

## Best Practices

### 1. Tag Strategy
- Use consistent tag naming conventions
- Create tag hierarchies (e.g., `api-v2.user-management.create`)
- Limit the number of tags per request (max 10-15)
- Document tag meanings and usage

### 2. Performance Considerations
- Tag rules are evaluated for every request
- Use specific patterns to minimize processing overhead
- Monitor proxy performance with complex tagging rules

### 3. Analytics and Reporting
- Design tags with reporting requirements in mind
- Create tag combinations for multi-dimensional analysis
- Regularly review and update tag definitions

### 4. Governance
- Establish tag ownership and maintenance processes
- Version control tag definitions
- Audit tag usage and effectiveness

## Troubleshooting

### Common Issues

1. **Tags Not Applied**
   - Verify tag rules are active
   - Check pattern matching syntax
   - Ensure rules are evaluated in correct order

2. **Performance Impact**
   - Reduce number of active tagging rules
   - Use more specific patterns
   - Monitor rule evaluation performance

3. **Query Performance**
   - Index tags in your analytics system
   - Use appropriate query operators
   - Consider tag aggregation strategies

### Debugging

Enable debug logging for tagging issues:

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

Check active tagging rules:

```bash
curl -X GET http://localhost:8080/api/Proxies/proxy_12345
```

## Advanced Tag Management

### Dynamic Tag Creation

Create tags based on request/response content:

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/tag \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Method": "GET",
      "Uri": "/api/search",
      "Request": {
        "Content": "*\"query\": \"*\"*"
      },
      "Tags": ["search-query", "user-generated-content"]
    }
  ]'
```

### Tag-Based Routing (Future Enhancement)

Tags can be used for conditional routing in advanced setups:

```json
{
  "TaggedCalls": [
    {
      "Method": "*",
      "Uri": "/api/premium/*",
      "Request": {
        "Headers": [
          {"Key": "X-User-Plan", "Values": ["premium"]}
        ]
      },
      "Tags": ["premium-traffic", "route-to-fast-cluster"]
    }
  ]
}
```

The Tagging feature provides the foundation for sophisticated traffic analysis, monitoring, and business intelligence in complex distributed systems, enabling data-driven insights and automated decision-making.
