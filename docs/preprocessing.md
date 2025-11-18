# Advanced Preprocessing

Slipka's preprocessing system provides extensible request/response transformation capabilities, allowing you to modify HTTP traffic before it reaches target services or after responses are received.

## Overview

Preprocessors are pluggable components that can transform requests and responses. They enable authentication header injection, request modification, response transformation, and custom processing logic.

## Built-in Preprocessors

### Header Authentication Preprocessor

Automatically injects authentication headers into requests.

**Configuration:**
```json
{
  "Type": "HeaderAuthenticationPreprocessor",
  "Config": {
    "headerName": "Authorization",
    "headerValue": "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
    "uriPatterns": ["/api/protected/*"]
  }
}
```

**Usage:**
```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/preprocessor \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Type": "HeaderAuthenticationPreprocessor",
      "Config": {
        "headerName": "Authorization",
        "headerValue": "Bearer your-token-here",
        "uriPatterns": ["/api/*"]
      }
    }
  ]'
```

### Session-Based Preprocessor

Maintains state across multiple requests within a session.

**Configuration:**
```json
{
  "Type": "SessionBasedPreprocessor",
  "Config": {
    "stateKey": "user-session",
    "initialState": {"authenticated": false},
    "rules": [...]
  }
}
```

## Custom Preprocessor Development

Create custom preprocessors by implementing the `IPreprocessor` interface:

```csharp
using Slipka.Preprocessors.Interfaces;
using System.Net.Http;
using System.Threading.Tasks;

public class CustomPreprocessor : AbstractPreprocessor
{
    private readonly string _customConfig;

    public CustomPreprocessor(string customConfig)
    {
        _customConfig = customConfig;
    }

    protected override Task ProcessRequestAsync(HttpRequestMessage request, Session session)
    {
        // Add custom request processing logic
        request.Headers.Add("X-Custom-Header", _customConfig);
        return Task.CompletedTask;
    }

    protected override Task ProcessResponseAsync(HttpResponseMessage response, Session session)
    {
        // Add custom response processing logic
        response.Headers.Add("X-Processed-By", "CustomPreprocessor");
        return Task.CompletedTask;
    }
}
```

## Configuration Examples

### API Key Injection

```bash
curl -X PUT http://localhost:8080/api/Proxies/proxy_12345/preprocessor \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Type": "HeaderAuthenticationPreprocessor",
      "Config": {
        "headerName": "X-API-Key",
        "headerValue": "your-api-key-here",
        "uriPatterns": ["/api/v2/*"]
      }
    }
  ]'
```

### Multi-Environment Authentication

```bash
# Development environment
curl -X PUT http://localhost:8080/api/Proxies/dev-proxy/preprocessor \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Type": "HeaderAuthenticationPreprocessor",
      "Config": {
        "headerName": "Authorization",
        "headerValue": "Bearer dev-token-123",
        "uriPatterns": ["*"]
      }
    }
  ]'

# Staging environment
curl -X PUT http://localhost:8080/api/Proxies/staging-proxy/preprocessor \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Type": "HeaderAuthenticationPreprocessor",
      "Config": {
        "headerName": "Authorization",
        "headerValue": "Bearer staging-token-456",
        "uriPatterns": ["*"]
      }
    }
  ]'
```

## Best Practices

1. **Security**: Store sensitive authentication data securely
2. **Performance**: Minimize processing overhead in preprocessors
3. **Testing**: Thoroughly test custom preprocessors
4. **Documentation**: Document preprocessor behavior and configuration
