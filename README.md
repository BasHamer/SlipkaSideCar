# Slipka
The untrustworthy proxy for testing service misbehavior

Slipka is a comprehensive reverse proxy solution designed specifically for testing distributed systems and microservices. Built with the understanding that services don't always behave perfectly, Slipka provides powerful tools to simulate various failure scenarios, intercept and modify traffic, and gather detailed insights into system behavior.

## Core Features

### 🏗️ [**Dynamic Proxy Management**](docs/dynamic-proxy-management.md)
- **On-Demand Proxy Creation**: Spin up lightweight proxy instances via REST API calls
- **Isolated Test Environments**: Each test can have its own proxy instance running in parallel
- **Automatic Port Management**: Dynamic port allocation from configurable ranges
- **Session-Based Persistence**: Proxy configurations persist for the duration of test sessions

### 🧪 **Traffic Manipulation & Testing**

#### [**Injection**](docs/injection.md) 🔄
Configure custom responses for specific request conditions to simulate various scenarios:
- Mock API responses before implementation
- Test error handling with custom status codes (400, 500, etc.)
- Simulate network delays and timeouts
- Inject malformed responses for robustness testing

#### [**Recording**](docs/recording.md) 📊
Capture and store traffic patterns for analysis and replay:
- Selective recording based on URL patterns, HTTP methods, or headers
- Full request/response body capture with metadata
- File download interception (perfect for Selenium testing)
- Session-based traffic aggregation

#### [**Tagging**](docs/tagging.md) 🏷️
Add metadata to traffic for enhanced reporting and analysis:
- Categorize requests by test scenarios
- Track performance metrics per operation type
- Enable detailed analytics and debugging
- Support for custom tag hierarchies

#### [**Decorating**](docs/decorating.md) 🎨
Automatically add identifiers to all proxied traffic:
- Inject correlation IDs for distributed tracing
- Add environment markers (test/staging/production)
- Include test identifiers for log aggregation
- Custom header injection for observability

### ⚙️ [**Advanced Preprocessing**](docs/preprocessing.md)
Extensible request/response transformation system with built-in preprocessors:
- **Header Authentication**: Automatic injection of API keys, bearer tokens, or custom headers
- **Session-Based Processing**: Stateful transformations across multiple requests
- **URI Pattern Matching**: Apply transformations selectively based on request paths
- **Plugin Architecture**: Custom preprocessor development support

### 🌐 **Proxy Architecture**

#### [**Static Proxies**](docs/static-proxies.md) 🔧
Pre-configured, long-running proxy instances defined in configuration:
- Always-on proxies for continuous testing environments
- Auto-start capability on application launch
- Production traffic mirroring with decorations
- Error simulation for staging environments

#### [**Reverse Proxy**](docs/reverse-proxy.md) 🔀
Multi-route reverse proxy for complex microservice architectures:
- Path-based routing (`/api/service1/*` → service1, `/api/service2/*` → service2)
- Authentication requirements per route
- Route-specific decorations and preprocessing
- **Error Masking**: Prevent stack traces and sensitive error details from leaking
- HTTPS support with configurable certificates

### 📊 **Observability & Monitoring**

#### [**Structured Logging**](docs/logging.md) 📝
Enhanced logging capabilities with correlation tracking:
- Correlation ID injection and propagation
- Structured log events with contextual metadata
- Configurable log levels and sinks
- Integration with distributed tracing systems

#### [**Health Checks**](docs/health-checks.md) ❤️
Comprehensive system health monitoring:
- MongoDB connection and performance checks
- Redis caching layer health validation
- Proxy instance status monitoring
- Static proxy availability verification

#### [**Session Management**](docs/session-management.md) 📈
Complete session lifecycle management:
- Session creation, configuration, and cleanup
- Traffic aggregation and analysis
- File storage for large payloads (GridFS integration)
- Configurable retention policies

### 🗄️ [**Data Persistence**](docs/data-persistence.md)
Robust data storage with multiple backend options:
- **MongoDB**: Primary data store for sessions, configurations, and metadata
- **Redis**: High-performance caching and session state management
- **GridFS**: Large file storage for request/response bodies
- **Automatic Cleanup**: Configurable data retention and cleanup loops

### 🔐 [**Security & Authentication**](docs/security.md)
Enterprise-grade security features:
- JWT-based API authentication
- Configurable issuer and audience validation
- Route-level authentication requirements
- Secure header preprocessing for authentication

### 🚀 [**Performance & Scalability**](docs/performance.md)
Built for high-performance testing scenarios:
- **Response Caching**: Configurable caching with invalidation support
- **Concurrent Sessions**: Support for multiple parallel proxy instances
- **Efficient Storage**: Optimized data structures and indexing
- **Resource Management**: Automatic cleanup and port recycling

## Quick Start

```bash
# Start Slipka with Docker
docker-compose up -d

# Create a proxy instance
curl -X POST http://localhost:8080/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{
    "TargetHost": "api.example.com",
    "TargetPort": 443,
    "TargetPortHttps": true,
    "InjectedCalls": [{
      "Method": "GET",
      "Uri": "/api/health",
      "StatusCode": "503",
      "Response": {
        "Content": "{\"status\": \"Service Unavailable\"}",
        "Headers": [{"Key": "Content-Type", "Values": ["application/json"]}]
      }
    }]
  }'
```

## Use Cases

- **Microservice Testing**: Simulate inter-service communication failures
- **API Development**: Mock endpoints before backend implementation
- **Load Testing**: Introduce controlled failures and delays
- **Integration Testing**: Capture and validate API interactions
- **Chaos Engineering**: Test system resilience under adverse conditions
- **Staging Environments**: Mirror production traffic with controlled interference
- **Debugging**: Add observability headers for log correlation
- **Security Testing**: Test authentication and authorization flows
- **Production Safety**: Mask sensitive error details while preserving correlation IDs

## Architecture

Slipka follows a modular architecture with clear separation of concerns:
- **API Layer**: RESTful endpoints for proxy management
- **Core Engine**: Proxy logic with injection, recording, and preprocessing
- **Persistence Layer**: MongoDB/Redis for data storage and caching
- **Configuration System**: Flexible configuration with validation
- **Health Monitoring**: Comprehensive system health checks
- **Extensibility**: Plugin architecture for custom preprocessors and integrations



