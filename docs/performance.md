# Performance & Scalability

Slipka is designed for high-performance testing scenarios with features like response caching, connection pooling, and optimized data structures to handle demanding workloads.

## Overview

Performance optimization includes caching layers, efficient data storage, connection management, and scalable architecture to support parallel testing and high-throughput environments.

## Caching Configuration

### Response Caching

Configure response caching for improved performance:

```json
{
  "Caching": {
    "Enabled": true,
    "DefaultTtl": "00:05:00",
    "MaxCacheSize": "100MB",
    "CacheKeyGenerator": "Default"
  }
}
```

### Redis Cache Settings

```json
{
  "RedisSettings": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "slipka",
    "DefaultDatabase": 0,
    "Enabled": true,
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000
  }
}
```

## Connection Management

### Connection Pooling

```json
{
  "ProxySettings": {
    "MaxConnectionsPerServer": 100,
    "ConnectionTimeout": "00:00:30",
    "KeepAliveTimeout": "00:02:00",
    "MaxResponseDrainSize": 1048576
  }
}
```

### HTTP Client Configuration

```csharp
// Optimized HttpClient configuration
var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = 100,
    ConnectTimeout = TimeSpan.FromSeconds(30),
    KeepAliveTimeout = TimeSpan.FromMinutes(2),
    EnableMultipleHttp2Connections = true
};

var client = new HttpClient(handler);
```

## Performance Monitoring

### Metrics Collection

```json
{
  "Metrics": {
    "Enabled": true,
    "Provider": "Prometheus",
    "Endpoint": "/metrics",
    "IncludeHistograms": true,
    "IncludeCounters": true
  }
}
```

### Key Metrics

- **Request Rate**: Requests per second by endpoint
- **Response Time**: P95, P99 response times
- **Error Rate**: Percentage of failed requests
- **Cache Hit Rate**: Cache effectiveness
- **Memory Usage**: Application memory consumption
- **Connection Pool Usage**: Active vs idle connections

## Scalability Features

### Horizontal Scaling

Deploy multiple Slipka instances behind a load balancer:

```yaml
# docker-compose.scale.yml
version: '3.8'
services:
  slipka:
    image: slipka/slipka:latest
    deploy:
      replicas: 3
    environment:
      - REDIS_CONNECTION_STRING=redis:6379
      - MONGODB_CONNECTION_STRING=mongodb://mongo:27017
```

### Database Sharding

Configure MongoDB sharding for high-volume data:

```javascript
// Enable sharding on database
sh.enableSharding("SlipkaDb")

// Shard collections by session ID
sh.shardCollection("SlipkaDb.sessions", { "_id": 1 })
sh.shardCollection("SlipkaDb.calls", { "SessionId": 1 })
```

### Redis Clustering

Configure Redis cluster for distributed caching:

```json
{
  "RedisSettings": {
    "ConnectionString": "redis-cluster:6379,redis-cluster:6380,redis-cluster:6381",
    "ClusterMode": true
  }
}
```

## Performance Optimization

### Indexing Strategy

```javascript
// Performance indexes
db.calls.createIndex({ "SessionId": 1, "CallNumber": 1 })
db.calls.createIndex({ "Timestamp": 1 })
db.calls.createIndex({ "Tags": 1 })
db.calls.createIndex({ "Method": 1, "Uri": 1 })

// Compound indexes for common queries
db.calls.createIndex({
  "SessionId": 1,
  "StatusCode": 1,
  "Duration": -1
})
```

### Query Optimization

```javascript
// Use covered queries where possible
db.calls.find(
  { SessionId: "session_123" },
  { CallNumber: 1, Method: 1, Uri: 1, StatusCode: 1 }
)

// Use aggregation pipelines efficiently
db.calls.aggregate([
  { $match: { SessionId: "session_123" } },
  { $sort: { Timestamp: -1 } },
  { $limit: 100 }
])
```

### Memory Management

```json
{
  "Memory": {
    "CacheSizeLimit": "512MB",
    "CompactionThreshold": "256MB",
    "GcSettings": {
      "ServerGc": true,
      "ConcurrentGc": true
    }
  }
}
```

## Load Testing

### Performance Benchmarks

```bash
# Basic load test
hey -n 10000 -c 100 http://localhost:8080/api/status

# Proxy throughput test
k6 run -e PROXY_URL=http://localhost:61710 proxy-throughput.js
```

### K6 Load Test Script

```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 200 }, // Ramp up to 200 users
    { duration: '5m', target: 200 }, // Stay at 200 users
    { duration: '2m', target: 0 },   // Ramp down to 0 users
  ],
};

export default function () {
  let response = http.get('http://localhost:61710/api/test');
  check(response, { 'status is 200': (r) => r.status === 200 });
}
```

## Resource Management

### CPU Optimization

```json
{
  "Threading": {
    "MinThreads": 100,
    "MaxThreads": 1000,
    "ThreadPoolSettings": {
      "MinWorkerThreads": 50,
      "MinCompletionPortThreads": 50
    }
  }
}
```

### Memory Limits

```json
{
  "ResourceLimits": {
    "MaxWorkingSet": "1GB",
    "MaxVirtualMemory": "2GB",
    "GcHeapHardLimit": "512MB"
  }
}
```

## Monitoring and Alerting

### Performance Alerts

```yaml
# Alert on high response times
- alert: HighResponseTime
  expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 2
  for: 5m
  labels:
    severity: warning

# Alert on high error rate
- alert: HighErrorRate
  expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.05
  for: 5m
  labels:
    severity: critical
```

### Dashboard Metrics

Key metrics to monitor:

- **Throughput**: Requests per second
- **Latency**: Response time percentiles (P50, P95, P99)
- **Error Rate**: Percentage of 4xx/5xx responses
- **Resource Usage**: CPU, memory, disk I/O
- **Cache Performance**: Hit rate, miss rate
- **Connection Pool**: Active/idle connections

## Best Practices

### 1. Capacity Planning
- Monitor resource usage trends
- Plan for peak load scenarios
- Implement auto-scaling where possible

### 2. Performance Testing
- Regular load testing of proxy configurations
- Test with realistic traffic patterns
- Validate performance under failure conditions

### 3. Optimization
- Use caching strategically
- Optimize database queries
- Implement connection pooling

### 4. Monitoring
- Set up comprehensive monitoring
- Define performance SLIs/SLOs
- Implement automated alerting

### 5. Scalability
- Design for horizontal scaling
- Use distributed caching
- Implement database sharding for high volume
