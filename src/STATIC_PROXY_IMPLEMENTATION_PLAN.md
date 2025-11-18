# Static Proxy Implementation Plan

## Overview

This document outlines the implementation plan for extending Slipka to support static proxy configuration with fixed ports that can be spun up at startup.

## Implementation Summary

### ✅ Completed Features

1. **Configuration Schema Design**
   - Added `StaticProxies` array to `appsettings.json`
   - Each static proxy has: Id, Port, TargetHost, TargetPort, Name, AutoStart flag
   - Support for pre-configured RecordedCalls, InjectedCalls, TaggedCalls, and Decorations

2. **Configuration Models**
   - `StaticProxyConfig`: Individual proxy configuration
   - `StaticProxySettings`: Collection of static proxy configurations
   - `StaticProxyValidator`: Validates port ranges, duplicates, and conflicts

3. **Static Proxy Manager**
   - `StaticProxyManager`: Service for managing static proxy lifecycle
   - Supports start/stop/restart operations
   - Provides status monitoring and health checks

4. **Startup Integration**
   - `StaticProxyInitializationService`: Hosted service that initializes static proxies on startup
   - Validation occurs before proxy creation
   - Failed validation prevents application startup

5. **API Extensions**
   - `GET /api/Proxies/static`: List all static proxies with status
   - `GET /api/Proxies/static/{id}`: Get specific static proxy status
   - `POST /api/Proxies/static/{id}/start|stop|restart`: Lifecycle management

6. **Port Conflict Resolution**
   - Static proxy ports are excluded from dynamic proxy port allocation
   - Validation prevents duplicate ports and out-of-range assignments
   - Port availability is checked at startup

7. **Health Monitoring**
   - `/health` endpoint with static proxy health checks
   - Monitors auto-start proxy status and port accessibility
   - Provides detailed health status and diagnostics

8. **Testing**
   - Unit tests for configuration validation
   - Test coverage for port validation, duplicate detection, and range checking

## Configuration Example

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
      ]
    }
  ]
}
```

## API Usage Examples

### List Static Proxies
```bash
curl http://localhost:4445/api/Proxies/static
```

### Start a Static Proxy
```bash
curl -X POST http://localhost:4445/api/Proxies/static/production-mirror/start
```

### Check Health
```bash
curl http://localhost:4445/health
```

## Key Benefits

1. **Pre-configured Environments**: Set up testing environments with fixed proxy configurations
2. **Long-running Proxies**: Static proxies can run for extended periods (default 1 year)
3. **Automated Startup**: Auto-start proxies begin immediately when Slipka starts
4. **Conflict Prevention**: Validation ensures no port conflicts between static and dynamic proxies
5. **Monitoring**: Health checks provide visibility into static proxy status
6. **Flexible Configuration**: Support for all existing Slipka features (recording, injection, tagging, decoration)

## Architecture Decisions

1. **Configuration-driven**: Static proxies are defined in configuration files for easy deployment
2. **Validation at startup**: Configuration is validated before any proxies start
3. **Separate management**: Static proxies use a dedicated manager distinct from dynamic proxies
4. **Health integration**: Leverages ASP.NET Core health checks for monitoring
5. **API consistency**: Static proxy management follows similar patterns to dynamic proxies

## Future Enhancements

1. **Configuration reloading**: Support for runtime configuration updates without restart
2. **Configuration sources**: Support for external configuration sources (environment variables, config servers)
3. **Advanced health checks**: More detailed health metrics and alerting
4. **Load balancing**: Support for multiple static proxies targeting the same service
5. **Configuration UI**: Web interface for managing static proxy configurations

## Testing Strategy

1. **Unit Tests**: Configuration validation, port management, lifecycle operations
2. **Integration Tests**: Full API testing with real proxy instances
3. **Health Check Tests**: Validation of monitoring functionality
4. **Configuration Tests**: Various configuration scenarios and edge cases

## Deployment Considerations

1. **Port planning**: Coordinate static proxy ports across environments
2. **Configuration management**: Use different configurations for dev/staging/prod
3. **Resource monitoring**: Monitor memory and CPU usage of long-running proxies
4. **Backup strategies**: Consider data retention policies for long-running proxies
