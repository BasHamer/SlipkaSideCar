# Slipka - The Untrustworthy Proxy

Slipka is a dynamically provisioning proxy service designed for testing distributed systems and microservices. It allows you to create lightweight proxy instances on-demand, making it perfect for parallel test execution where each test needs its own isolated proxy environment.

## Overview

Slipka serves as a "man-in-the-middle" proxy that can intercept, modify, record, and inject responses for HTTP traffic. It's particularly useful for:

- **Testing service resilience** by simulating error conditions, slow responses, or network failures
- **Mocking APIs** before they're implemented
- **Recording traffic** for analysis and debugging
- **Adding traceability** through request decoration and tagging
- **Authentication offloading** with extensible preprocessors for session management
- **Parallel testing** with isolated proxy instances per test

## Architecture

### Core Components

- **Main API Server**: Runs on port 4445, provides REST API for proxy management
- **Dynamic Proxies**: Individual proxy instances created on-demand, each running on a unique port
- **MongoDB Storage**: Persists sessions, calls, and recorded data
- **GridFS**: Stores request/response bodies for recorded calls

### Project Structure

```
src/
├── Slipka/                          # Main API application
│   ├── Controllers/                 # REST API controllers
│   │   ├── ProxiesController.cs     # Proxy lifecycle management
│   │   ├── SessionsApiController.cs # Session data retrieval
│   │   └── SessionsController.cs    # Request/response replay
│   ├── DomainObjects/               # Core business entities
│   │   ├── Session.cs               # Proxy session configuration
│   │   └── Call.cs                  # Individual HTTP call data
│   ├── Proxy/                       # Proxy implementation
│   │   ├── Proxy.cs                 # Individual proxy host
│   │   ├── ProxyHandler.cs          # Request processing logic
│   │   └── ProxyStartup.cs          # Proxy configuration
│   ├── Repositories/                # Data persistence
│   │   ├── SessionRepository.cs     # Session CRUD operations
│   │   ├── MessageRepository.cs     # HTTP message storage
│   │   └── FileRepository.cs        # Binary content storage
│   ├── Configuration/               # Settings management
│   └── ApiArguments/                # API request/response models
├── Microsoft.AspNetCore.Proxy/      # Proxy middleware library
├── PossumLabs.Specflow.Slipka/      # SpecFlow integration
└── Extensions.cs                    # Shared utilities
```

## Key Features

### 1. Dynamic Proxy Provisioning

Create proxy instances programmatically with specific configurations:

```http
POST /api/Proxies
Content-Type: application/json

{
  "targetHost": "api.example.com",
  "targetPort": 443,
  "openFor": "01:00:00",
  "retainedFor": "30:00:00:00"
}
```

**Response:**
```json
{
  "id": "session-guid",
  "proxyPort": 61710,
  "targetHost": "api.example.com",
  "targetPort": 443,
  "leaveProxyOpenUntil": "2025-11-17T12:00:00Z",
  "retainDataUntil": "2025-12-17T12:00:00Z"
}
```

### 2. Static Proxy Configuration

Configure proxies with fixed ports that start automatically on application startup. Define static proxies in `appsettings.json`:

```json
{
  "StaticProxies": [
    {
      "Id": "production-mirror",
      "Port": 61800,
      "TargetHost": "api.production.com",
      "TargetPort": 443,
      "Name": "Production API Mirror",
      "AutoStart": true,
      "RecordedCalls": [
        {
          "Method": "POST",
          "Uri": "/api/orders",
          "Tags": ["production-order"]
        }
      ],
      "Decorations": [
        {
          "Key": "X-Proxy-Source",
          "Values": ["slipka-production-mirror"]
        }
      ],
      "Preprocessors": [
        {
          "Type": "HeaderAuthentication",
          "Config": {
            "headerName": "X-API-Key",
            "headerValue": "production-api-key-123",
            "uriPatterns": ["/api/.*"]
          }
        }
      ]
    }
  ]
}
```

Manage static proxies via API:

```http
# List all static proxies
GET /api/Proxies/static

# Get specific static proxy status
GET /api/Proxies/static/{id}

# Start/stop/restart static proxies
POST /api/Proxies/static/{id}/start
POST /api/Proxies/static/{id}/stop
POST /api/Proxies/static/{id}/restart
```

### 3. Response Injection

Inject custom responses for specific requests based on matching criteria:

```http
PUT /api/Proxies/{sessionId}/inject
Content-Type: application/json

{
  "method": "GET",
  "uri": "/api/users/.*",
  "statusCode": "500",
  "duration": 2000,
  "response": {
    "content": "{\"error\": \"Internal Server Error\"}",
    "headers": [
      {
        "key": "Content-Type",
        "values": ["application/json"]
      }
    ]
  },
  "tags": ["error-simulation"]
}
```

### 3. Traffic Recording

Capture and store HTTP traffic for later analysis:

```http
PUT /api/Proxies/{sessionId}/record
Content-Type: application/json

{
  "method": "POST",
  "uri": "/api/orders",
  "request": {
    "headers": [
      {
        "key": "Authorization",
        "values": [".*"]
      }
    ]
  }
}
```

### 4. Request Decoration

Add headers to all requests passing through the proxy:

```http
PUT /api/Proxies/{sessionId}/decorate
Content-Type: application/json

{
  "key": "X-Test-Id",
  "values": ["integration-test-123"]
}
```

### 5. Custom Preprocessors

Add custom request preprocessors for authentication and session management. Preprocessors can be stateful and lazy-loaded. The system supports both built-in preprocessors and custom user-defined preprocessors.

#### Built-in Preprocessors

##### Header Authentication
```http
PUT /api/Proxies/{sessionId}/preprocessor
Content-Type: application/json

{
  "type": "HeaderAuthentication",
  "config": {
    "headerName": "Authorization",
    "headerValue": "Bearer eyJ0eXAi...",
    "uriPatterns": ["/api/.*"]
  }
}
```

##### Session-Based Authentication
```http
PUT /api/Proxies/{sessionId}/preprocessor
Content-Type: application/json

{
  "type": "SessionBased",
  "config": {
    "loginUrl": "https://auth.example.com/oauth/token",
    "loginMethod": "POST",
    "loginBody": "grant_type=client_credentials&client_id=123&client_secret=456",
    "tokenExtractor": "json:access_token",
    "headerTemplate": "Bearer {token}",
    "sessionDuration": 3600,
    "uriPatterns": ["/api/secure/.*"]
  }
}
```

#### Custom Preprocessors

Slipka supports custom preprocessor implementations through a plugin system.

##### Configuration
Add custom preprocessor assemblies to `appsettings.json`:

```json
{
  "PreprocessorSettings": {
    "CustomPreprocessorAssemblies": [
      "path/to/MyCustomPreprocessors.dll"
    ],
    "CustomPreprocessorTypes": {
      "MyCustomAuth": "MyNamespace.MyCustomPreprocessor, MyCustomPreprocessors"
    }
  }
}
```

##### Creating Custom Preprocessors

Implement the `IPreprocessor` interface:

```csharp
using Slipka.Preprocessors.Interfaces;
using Slipka.Preprocessors.Base;
using System.Net.Http;
using System.Threading.Tasks;

namespace MyCustomPreprocessors
{
    [PreprocessorTypeName("MyCustomAuth")]
    public class MyCustomPreprocessor : AbstractPreprocessor
    {
        private readonly string _apiKey;

        public MyCustomPreprocessor(string apiKey)
        {
            _apiKey = apiKey;
        }

        protected override async Task ProcessRequestAsync(HttpRequestMessage request, Session session)
        {
            // Custom authentication logic
            AddHeader(request, "X-API-Key", _apiKey);

            // Add additional custom logic here
            if (request.RequestUri.PathAndQuery.Contains("/admin"))
            {
                AddHeader(request, "X-Admin-Token", await GetAdminTokenAsync());
            }
        }

        private async Task<string> GetAdminTokenAsync()
        {
            // Custom token retrieval logic
            return "admin-token-123";
        }
    }
}
```

##### Using Custom Preprocessors
```http
PUT /api/Proxies/{sessionId}/preprocessor
Content-Type: application/json

{
  "type": "MyCustomAuth",
  "config": {
    "apiKey": "my-secret-key"
  }
}
```

##### Preprocessor Registry API

For advanced scenarios, you can register custom preprocessors programmatically:

```csharp
// Register a custom preprocessor type
services.AddSingleton<IPreprocessorRegistry>(provider =>
{
    var registry = new PreprocessorRegistry();

    // Register via type
    registry.RegisterPreprocessorType("MyType", typeof(MyCustomPreprocessor));

    // Or register via factory function
    var factory = provider.GetRequiredService<IPreprocessorFactory>();
    if (factory is PreprocessorFactory concreteFactory)
    {
        concreteFactory.RegisterPreprocessorType("MyType", async (message) =>
        {
            // Custom creation logic
            return new MyCustomPreprocessor(message.Config["apiKey"]?.ToString());
        });
    }

    return registry;
});
```

### 6. Call Tagging

Automatically tag calls based on matching criteria for reporting:

```http
PUT /api/Proxies/{sessionId}/tag
Content-Type: application/json

{
  "method": "GET",
  "uri": "/api/slow-endpoint",
  "duration": 1000,
  "tags": ["slow-call", "performance-test"]
}
```

## API Reference

### Proxy Management

#### Create Proxy
```http
POST /api/Proxies
```

Creates a new proxy instance targeting the specified host/port.

**Request Body:**
- `targetHost` (string, required): Target server hostname
- `targetPort` (integer, optional): Target server port (default: 80)
- `openFor` (string, optional): How long proxy stays open (default: "01:00:00")
- `retainedFor` (string, optional): How long data is retained (default: "31:00:00:00")
- `preprocessors` (array, optional): List of preprocessor configurations

#### Delete Proxy
```http
DELETE /api/Proxies/{sessionId}
```

Stops and removes a proxy instance.

### Session Data Retrieval

#### Get Session
```http
GET /api/SessionsApi/{sessionId}
```

Retrieves complete session information including all calls.

#### Get Sessions
```http
GET /api/SessionsApi?tag={tag}
```

Lists all sessions, optionally filtered by tag.

#### Get Session Calls
```http
GET /api/SessionsApi/{sessionId}/calls?tag={tag}&recorded={true}&minimumDuration={ms}
```

Retrieves calls for a session with optional filtering.

#### Delete Sessions
```http
DELETE /api/SessionsApi/{sessionId}
DELETE /api/SessionsApi
```

Removes specific session or all sessions.

### Request/Response Replay

#### Get Request
```http
GET /api/Sessions/{sessionId}/request/{callNumber}
```

Retrieves the original request body for a recorded call.

#### Get Response
```http
GET /api/Sessions/{sessionId}/response/{callNumber}
```

Retrieves the original response body for a recorded call.

## Health Monitoring

Slipka provides health check endpoints for monitoring proxy status:

```http
# Overall health check
GET /health

# Health check response example
{
  "status": "Healthy",
  "checks": [
    {
      "name": "Static Proxies",
      "status": "Healthy",
      "description": "All 2 auto-start static proxies are running and healthy"
    }
  ]
}
```

Static proxies are automatically monitored to ensure:
- Auto-start proxies are running
- Proxy ports are accessible
- Configuration matches runtime state

## Configuration

### Default Settings

```json
{
  "ProxySettings": {
    "FirstPort": 61710,
    "LastPort": 61920,
    "DefaultOpenFor": "01:00:00",
    "MaxOpenFor": "2:00:00:00",
    "DefaultRetainedFor": "31:00:00:00",
    "MaxRetainedFor": "90:00:00:00",
    "GridFsCleanupLoop": 50000,
    "ProxyPersistanceLoop": 5000
  },
  "StaticProxies": [
    {
      "Id": "unique-proxy-id",
      "Port": 61800,
      "TargetHost": "api.example.com",
      "TargetPort": 443,
      "Name": "Descriptive Name",
      "AutoStart": true,
      "OpenFor": "8760:00:00",
      "RetainedFor": "365:00:00:00"
    }
  ],
  "MongoSettings": {
    "ConnectionString": "mongodb://db/",
    "Database": "SlipkaDb",
    "ChunkSizeBytes": 1048576
  }
}
```

### Authentication Settings

Slipka supports JWT token validation for reverse proxy routes. Authentication is configured in the `Authentication` section:

```json
{
  "Authentication": {
    "ValidateIssuer": true,
    "Issuer": "your-issuer",
    "ValidateAudience": true,
    "Audience": "your-audience",
    "ValidateLifetime": true,
    "ValidateIssuerSigningKey": true,
    "IssuerSigningKey": "",
    "IssuerSigningKeyEnvironmentVariable": "SLIPKA_JWT_SIGNING_KEY",
    "ClockSkewMinutes": 5
  }
}
```

#### JWT Signing Key Configuration

The JWT signing key can be provided through an environment variable for better security:

1. **Environment Variable (Recommended)**: Set `SLIPKA_JWT_SIGNING_KEY` environment variable with your secret key
2. **Configuration Fallback**: If the environment variable is not set, the key can be specified in `IssuerSigningKey`

The key can be:
- A plain text string (minimum 256 bits recommended)
- A base64-encoded binary key

Example environment variable setup:
```bash
export SLIPKA_JWT_SIGNING_KEY="your-256-bit-secret-key-here"
```

#### Reverse Proxy Authentication

Routes in the reverse proxy can require authentication by setting `RequiresAuthentication: true`:

```json
{
  "ReverseProxy": {
    "Routes": [
      {
        "Id": "public-api",
        "Path": "/api/public/*",
        "TargetHost": "localhost",
        "TargetPort": 3000,
        "RequiresAuthentication": false
      },
      {
        "Id": "secure-api",
        "Path": "/api/secure/*",
        "TargetHost": "localhost",
        "TargetPort": 3001,
        "RequiresAuthentication": true
      }
    ]
  }
}
```

Routes requiring authentication will validate JWT tokens in the `Authorization` header (Bearer format).

### Port Range
- Dynamic proxies are assigned ports from 61710 to 61920 (210 ports available)
- Static proxies can use any port within this range with fixed assignments
- Ports are automatically assigned for dynamic proxies and checked for availability
- Static proxy ports are validated at startup to prevent conflicts

### Time Limits
- **Dynamic Proxy Open Time**: Default 1 hour, maximum 2 days
- **Dynamic Proxy Data Retention**: Default 31 days, maximum 90 days
- **Static Proxy Open Time**: Default 1 year (for long-running proxies)
- **Static Proxy Data Retention**: Default 1 year (for long-term analysis)

## Matching Logic

Call templates use flexible matching with regex support:

- **Method**: Exact string match or null for any method
- **URI**: Regex pattern matching request path
- **Status Code**: Regex pattern for HTTP status codes
- **Duration**: Maximum duration threshold
- **Headers**: Presence of specific header key/value combinations

## Data Storage

### MongoDB Collections
- **Sessions**: Proxy session configurations and metadata
- **Messages**: HTTP headers for requests/responses
- **Calls**: HTTP call metadata (method, URI, timing, etc.)

### GridFS
- Request/response bodies stored as binary data
- Automatic cleanup based on retention policies

## Usage Examples

### Basic Proxy Setup
```bash
# Start Slipka service
docker-compose up -d

# Create proxy for api.example.com
curl -X POST http://localhost:4445/api/Proxies \
  -H "Content-Type: application/json" \
  -d '{"targetHost": "api.example.com", "targetPort": 443}'

# Use proxy at assigned port (e.g., 61710)
curl -x http://localhost:61710 https://api.example.com/users
```

### Error Simulation
```bash
# Inject 500 error for user API
curl -X PUT http://localhost:4445/api/Proxies/{sessionId}/inject \
  -H "Content-Type: application/json" \
  -d '{
    "method": "GET",
    "uri": "/api/users/.*",
    "statusCode": "500",
    "response": {
      "content": "{\"error\": \"Service Unavailable\"}",
      "headers": [{"key": "Content-Type", "values": ["application/json"]}]
    }
  }'
```

### Traffic Recording
```bash
# Record all POST requests to /api/orders
curl -X PUT http://localhost:4445/api/Proxies/{sessionId}/record \
  -H "Content-Type: application/json" \
  -d '{
    "method": "POST",
    "uri": "/api/orders"
  }'
```

## Integration

### SpecFlow Integration
The `PossumLabs.Specflow.Slipka` package provides SpecFlow step definitions for easy integration with BDD tests.

### Docker Deployment
Use the provided `docker-compose.yml` for containerized deployment with MongoDB.

## Development

### Building
```bash
cd src
dotnet build Slipka.Docker.sln
```

### Running
```bash
cd src/Slipka
dotnet run
```

### Testing
```bash
dotnet test Slipka.Tests.sln
```

## Contributing

See `TODO.txt` for planned improvements and feature requests.

## License

See `LICENSE.txt` in the Microsoft.AspNetCore.Proxy directory.
