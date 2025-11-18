# Session Management

Slipka's session management system tracks and persists proxy configurations, traffic data, and metadata throughout the lifecycle of proxy instances.

## Overview

Sessions provide context and persistence for all proxy operations, enabling data correlation, historical analysis, and lifecycle management.

## API Endpoints

### Get Sessions
**Endpoint:** `GET /api/Sessions`

Returns all active sessions.

### Get Session Details
**Endpoint:** `GET /api/Sessions/{sessionId}`

Returns detailed information about a specific session.

### Get Session Calls
**Endpoint:** `GET /api/Sessions/{sessionId}/calls`

Returns all recorded calls for a session.

### Get Specific Call
**Endpoint:** `GET /api/Sessions/{sessionId}/calls/{callNumber}`

Returns details of a specific call.

### Download Call Content
**Endpoint:** `GET /api/Sessions/{sessionId}/calls/{callNumber}/request`

**Endpoint:** `GET /api/Sessions/{sessionId}/calls/{callNumber}/response`

Downloads request or response content.

## Session Lifecycle

1. **Creation**: Session created when proxy is instantiated
2. **Activity**: Traffic data recorded and associated with session
3. **Persistence**: Session data retained based on configuration
4. **Cleanup**: Automatic cleanup after retention period

## Configuration

Session retention is configured in `appsettings.json`:

```json
{
  "ProxySettings": {
    "DefaultRetainedFor": "31:00:00:00",
    "MaxRetainedFor": "90:00:00:00",
    "GridFsCleanupLoop": 50000,
    "ProxyPersistanceLoop": 5000
  }
}
```

## Usage Examples

### List Active Sessions

```bash
curl -X GET http://localhost:8080/api/Sessions
```

**Response:**
```json
[
  {
    "id": "session_abc123",
    "createdAt": "2025-11-18T14:30:00Z",
    "proxyId": "proxy_12345",
    "callCount": 42,
    "tags": ["integration-test"],
    "retainedUntil": "2025-12-19T14:30:00Z"
  }
]
```

### Get Session Details

```bash
curl -X GET http://localhost:8080/api/Sessions/session_abc123
```

**Response:**
```json
{
  "id": "session_abc123",
  "createdAt": "2025-11-18T14:30:00Z",
  "proxyId": "proxy_12345",
  "status": "Active",
  "callCount": 42,
  "dataSize": 1024000,
  "tags": ["integration-test"],
  "retainedUntil": "2025-12-19T14:30:00Z"
}
```

### Get Recorded Calls

```bash
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls"
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
    "tags": ["user-operations"]
  }
]
```

### Download Call Content

```bash
# Download request body
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls/1/request" \
  -o request-body.json

# Download response body
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls/1/response" \
  -o response-body.json
```

## Data Retention

### Automatic Cleanup

Sessions are automatically cleaned up based on retention policies:

- **Default Retention**: 31 days
- **Maximum Retention**: 90 days
- **Cleanup Frequency**: Every 50 seconds (configurable)

### Manual Cleanup

Override retention for specific sessions:

```bash
# Extend retention
curl -X PATCH "http://localhost:8080/api/Sessions/session_abc123" \
  -H "Content-Type: application/json" \
  -d '{"retainedUntil": "2026-01-18T14:30:00Z"}'
```

## Session Analytics

### Traffic Analysis

```bash
# Count requests by method
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq 'group_by(.method) | map({method: .[0].method, count: length})'

# Find slowest requests
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq 'sort_by(.duration) | reverse | .[0:10] | map({uri, duration, statusCode})'
```

### Error Analysis

```bash
# Find failed requests
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '.[] | select(.statusCode >= 400) | {uri, statusCode, duration}'
```

### Performance Trends

```bash
# Calculate average response time
curl -X GET "http://localhost:8080/api/Sessions/session_abc123/calls" | \
  jq '[.[] | .duration] | add / length'
```

## Best Practices

1. **Retention Planning**: Set appropriate retention periods based on use case
2. **Data Management**: Monitor storage usage and plan for growth
3. **Performance**: Optimize queries for large session datasets
4. **Security**: Implement access controls for sensitive session data
5. **Backup**: Consider backup strategies for critical session data
