# Preprocessor Implementation Plan

## Overview

This document outlines the implementation plan for adding custom pre-processors to Slipka that enable authentication and session management for external APIs. Preprocessors will be stateful and lazy-loaded, only creating authentication tokens when APIs are actually used.

## Architecture

### Core Interfaces

#### IPreprocessor
```csharp
public interface IPreprocessor
{
    string Id { get; }
    Task ProcessAsync(HttpRequestMessage request, Session session);
    bool IsEnabled { get; set; }
}
```

#### IStatefulPreprocessor
```csharp
public interface IStatefulPreprocessor : IPreprocessor
{
    bool IsInitialized { get; }
    Task InitializeAsync(Session session);
    Task CleanupAsync();
}
```

### Base Classes

#### AbstractPreprocessor
- Provides common functionality like ID generation
- Implements basic lifecycle management
- Handles error logging

#### HeaderAuthenticationPreprocessor
- Simple preprocessor that adds authentication headers
- Example: Bearer token, API key, Basic auth
- Not stateful - headers are static

#### SessionBasedPreprocessor
- Manages complex authentication flows
- Handles login, token refresh, session cookies
- Implements lazy loading and state management
- Example: OAuth2 flow, custom login API

### Key Design Decisions

1. **Lazy Loading**: Preprocessors only initialize when first request matches their criteria
2. **State Management**: Session data stored in MongoDB with preprocessor-specific collections
3. **Thread Safety**: All preprocessor operations are thread-safe
4. **Error Handling**: Failed preprocessing doesn't break the request flow
5. **Configuration**: Preprocessors configured via API similar to decorations

## Implementation Phases

### Phase 1: Core Architecture
- [ ] Create IPreprocessor and IStatefulPreprocessor interfaces
- [ ] Implement AbstractPreprocessor base class
- [ ] Add Preprocessors collection to Session domain object
- [ ] Create PreprocessorMessage API argument class

### Phase 2: Basic Preprocessors
- [ ] HeaderAuthenticationPreprocessor implementation
- [ ] SessionBasedPreprocessor implementation
- [ ] Basic authentication examples (Bearer, API Key)

### Phase 3: Integration
- [ ] Modify ProxyHandler to execute preprocessors
- [ ] Add preprocessor API endpoints
- [ ] Update CreateProxyMessage to include preprocessors
- [ ] Add static proxy preprocessor support

### Phase 4: Advanced Features
- [ ] Lazy loading implementation
- [ ] State persistence and recovery
- [ ] Token refresh mechanisms
- [ ] Preprocessor chaining and ordering

### Phase 5: Testing & Documentation
- [ ] Unit tests for all preprocessor types
- [ ] Integration tests with real APIs
- [ ] Documentation and examples

## API Design

### Configure Preprocessor
```http
PUT /api/Proxies/{sessionId}/preprocessor
Content-Type: application/json

{
  "type": "HeaderAuthentication",
  "config": {
    "headerName": "Authorization",
    "headerValue": "Bearer eyJ0eXAi..."
  }
}
```

### Complex Session Preprocessor
```http
PUT /api/Proxies/{sessionId}/preprocessor
Content-Type: application/json

{
  "type": "SessionBased",
  "config": {
    "loginUrl": "https://api.example.com/login",
    "loginMethod": "POST",
    "loginBody": "{\"username\":\"user\",\"password\":\"pass\"}",
    "tokenExtractor": "json:access_token",
    "headerTemplate": "Bearer {token}",
    "sessionDuration": "3600"
  }
}
```

## File Structure

```
src/Slipka/
├── Preprocessors/
│   ├── Interfaces/
│   │   ├── IPreprocessor.cs
│   │   └── IStatefulPreprocessor.cs
│   ├── Base/
│   │   └── AbstractPreprocessor.cs
│   ├── Implementations/
│   │   ├── HeaderAuthenticationPreprocessor.cs
│   │   └── SessionBasedPreprocessor.cs
│   └── PreprocessorFactory.cs
├── ApiArguments/
│   └── PreprocessorMessage.cs
└── DomainObjects/
    └── Session.cs (modified)
```

## Configuration Examples

### Static Proxy with Preprocessor
```json
{
  "StaticProxies": [
    {
      "Id": "authenticated-api",
      "Port": 61801,
      "TargetHost": "api.example.com",
      "TargetPort": 443,
      "Preprocessors": [
        {
          "Type": "SessionBased",
          "Config": {
            "loginUrl": "https://api.example.com/oauth/token",
            "loginMethod": "POST",
            "loginBody": "grant_type=client_credentials&client_id=123&client_secret=456",
            "tokenExtractor": "json:access_token",
            "headerTemplate": "Bearer {token}"
          }
        }
      ]
    }
  ]
}
```

## Benefits

1. **Offloaded Authentication**: No need for clients to handle auth tokens
2. **Centralized Management**: All authentication logic managed in one place
3. **Lazy Loading**: Resources only consumed when APIs are actually used
4. **Stateful Sessions**: Complex authentication flows supported
5. **Test-Friendly**: Easy to mock authenticated services in tests

## Considerations

- **Security**: Sensitive auth data should be encrypted in storage
- **Performance**: Preprocessor execution adds latency to requests
- **Error Handling**: Failed auth should not break legitimate requests
- **Monitoring**: Need to track preprocessor performance and failures
- **Compatibility**: Must work with existing decoration and injection features
