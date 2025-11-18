# Data Persistence

Slipka uses MongoDB as the primary data store with Redis for caching and session state management, providing robust data persistence for proxy configurations, traffic recordings, and metadata.

## Overview

The persistence layer handles storage and retrieval of all proxy-related data including sessions, traffic recordings, configurations, and large file content.

## Architecture

### MongoDB (Primary Store)
- **Sessions**: Proxy session data and metadata
- **Calls**: Recorded HTTP traffic data
- **Configurations**: Proxy and preprocessor configurations
- **Metadata**: Tags, correlation data, and analytics

### Redis (Cache & State)
- **Session State**: Active proxy session information
- **Caching**: Response caching with TTL
- **Rate Limiting**: Request rate limiting data
- **Temporary Data**: Short-lived operational data

### GridFS (Large Files)
- **Request/Response Bodies**: Large content storage
- **File Downloads**: Selenium and testing artifacts
- **Binary Data**: Images, documents, and other binary content

## Configuration

### MongoDB Configuration

```json
{
  "MongoSettings": {
    "ConnectionString": "mongodb://db/",
    "Database": "SlipkaDb",
    "ChunkSizeBytes": 1048576
  }
}
```

### Redis Configuration

```json
{
  "RedisSettings": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "slipka",
    "DefaultDatabase": 0,
    "Enabled": true
  }
}
```

## Data Models

### Session Document

```javascript
{
  _id: ObjectId("507f1f77bcf86cd799439011"),
  Id: "session_abc123",
  CreatedAt: ISODate("2025-11-18T14:30:00Z"),
  ProxyId: "proxy_12345",
  Status: "Active",
  CallCount: 42,
  DataSize: 1024000,
  Tags: ["integration-test"],
  RetainedUntil: ISODate("2025-12-19T14:30:00Z")
}
```

### Call Document

```javascript
{
  _id: ObjectId("507f1f77bcf86cd799439012"),
  SessionId: "session_abc123",
  CallNumber: 1,
  Method: "GET",
  Uri: "/api/users/123",
  StatusCode: 200,
  Duration: 245,
  Timestamp: ISODate("2025-11-18T14:30:15Z"),
  RequestHeaders: [...],
  ResponseHeaders: [...],
  RequestContentId: ObjectId("507f1f77bcf86cd799439013"),
  ResponseContentId: ObjectId("507f1f77bcf86cd799439014"),
  Tags: ["user-operations"]
}
```

## Usage Examples

### Query Sessions

```javascript
// Find active sessions
db.sessions.find({ Status: "Active" })

// Find sessions by tags
db.sessions.find({ Tags: "integration-test" })

// Find sessions created in last 24 hours
db.sessions.find({
  CreatedAt: {
    $gte: new Date(Date.now() - 24 * 60 * 60 * 1000)
  }
})
```

### Query Calls

```javascript
// Find calls by session
db.calls.find({ SessionId: "session_abc123" })

// Find error responses
db.calls.find({ StatusCode: { $gte: 400 } })

// Find slow requests
db.calls.find({ Duration: { $gte: 5000 } })

// Find calls by tags
db.calls.find({ Tags: "performance-issue" })
```

### Aggregation Examples

```javascript
// Count requests by method
db.calls.aggregate([
  { $group: { _id: "$Method", count: { $sum: 1 } } }
])

// Average response time by endpoint
db.calls.aggregate([
  {
    $group: {
      _id: "$Uri",
      avgDuration: { $avg: "$Duration" },
      count: { $sum: 1 }
    }
  }
])

// Error rate by status code
db.calls.aggregate([
  {
    $group: {
      _id: "$StatusCode",
      count: { $sum: 1 }
    }
  },
  {
    $match: { "_id": { $gte: 400 } }
  }
])
```

## Data Management

### Cleanup Operations

```javascript
// Remove expired sessions
db.sessions.deleteMany({
  RetainedUntil: { $lt: new Date() }
})

// Remove orphaned calls
db.calls.deleteMany({
  SessionId: {
    $nin: db.sessions.distinct("Id")
  }
})

// Clean up GridFS chunks
db.fs.chunks.deleteMany({
  uploadDate: { $lt: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000) }
})
```

### Indexing Strategy

```javascript
// Session indexes
db.sessions.createIndex({ Status: 1 })
db.sessions.createIndex({ CreatedAt: 1 })
db.sessions.createIndex({ Tags: 1 })

// Call indexes
db.calls.createIndex({ SessionId: 1 })
db.calls.createIndex({ StatusCode: 1 })
db.calls.createIndex({ Duration: 1 })
db.calls.createIndex({ Tags: 1 })
db.calls.createIndex({ Timestamp: 1 })

// Compound indexes for common queries
db.calls.createIndex({ SessionId: 1, CallNumber: 1 })
db.calls.createIndex({ Method: 1, Uri: 1 })
```

## Backup and Recovery

### MongoDB Backup

```bash
# Create backup
mongodump --db SlipkaDb --out /backup/slipka-$(date +%Y%m%d)

# Restore from backup
mongorestore --db SlipkaDb /backup/slipka-20251118
```

### Redis Backup

```bash
# Create Redis backup
redis-cli save

# Copy dump file
cp /var/lib/redis/dump.rdb /backup/redis-$(date +%Y%m%d).rdb
```

## Monitoring

### Storage Metrics

```javascript
// Database size
db.stats()

// Collection sizes
db.sessions.stats()
db.calls.stats()

// GridFS usage
db.fs.files.count()
db.fs.chunks.stats()
```

### Performance Monitoring

```javascript
// Slow queries
db.system.profile.find().sort({ ts: -1 }).limit(10)

// Index usage
db.calls.aggregate([
  { $indexStats: {} }
])
```

## Best Practices

1. **Indexing**: Create appropriate indexes for query patterns
2. **Data Retention**: Implement automated cleanup policies
3. **Backup**: Regular backups of critical data
4. **Monitoring**: Monitor storage growth and performance
5. **Sharding**: Consider sharding for high-volume deployments
6. **Compression**: Enable MongoDB compression for storage efficiency
