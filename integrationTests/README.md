# Slipka Integration Tests

This solution contains comprehensive integration tests for the Slipka proxy service. The tests are designed to run against a running Slipka Docker container and validate all features end-to-end.

## Overview

The integration tests follow a black-box testing approach, making HTTP calls to the Slipka API and validating functionality without referencing any internal DLLs. The test suite includes:

- **Test API Server**: A minimal ASP.NET Core API that provides endpoints for testing proxy behavior
- **Comprehensive Coverage**: Tests for all major Slipka features
- **Docker Integration**: Tests assume Slipka is running in Docker containers
- **Automatic Cleanup**: Tests clean up after themselves to avoid interference

## Project Structure

```
integrationTests/
├── Slipka.IntegrationTests.sln              # Solution file
├── Slipka.IntegrationTests/                 # Main test project
│   ├── Fixtures/                            # Test fixtures and base classes
│   │   ├── IntegrationTestBase.cs          # Base class for all integration tests
│   │   ├── TestConfiguration.cs            # Configuration management
│   │   └── TestApiFixture.cs               # Test API server management
│   ├── TestApi/                            # Test API server project
│   │   ├── Controllers/TestController.cs   # Test endpoints
│   │   └── Program.cs                      # Test API entry point
│   └── Tests/                              # Test categories
│       ├── ProxyManagement/                # Dynamic and static proxy tests
│       ├── ResponseInjection/              # Response injection tests
│       ├── TrafficRecording/               # Traffic recording tests
│       ├── RequestDecoration/              # Request decoration tests
│       ├── Preprocessors/                  # Preprocessor tests
│       ├── CallTagging/                    # Call tagging tests
│       ├── SessionApi/                     # Session data retrieval tests
│       ├── HealthMonitoring/               # Health check tests
│       └── ErrorScenarios/                 # Error condition tests
├── appsettings.json                        # Test configuration
└── README.md                               # This file
```

## Prerequisites

1. **Docker and Docker Compose** installed and running
2. **.NET 8.0 SDK** installed
3. **Slipka containers running** (see setup instructions below)

## Setup Instructions

### 1. Start Slipka Services

From the root project directory:

```bash
# Build and start the Slipka services
docker-compose up -d

# Wait for services to be healthy
docker-compose ps
```

Verify that the following services are running:
- `slipka` (port 4445)
- `db` (MongoDB, port 27017)
- `redis` (port 6379)

### 2. Run Integration Tests

```bash
# Navigate to integration tests directory
cd integrationTests

# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "ProxyManagement"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### 3. Test Configuration

The tests are configured via `appsettings.json` in the test project:

```json
{
  "Slipka": {
    "BaseUrl": "http://localhost:4445",
    "TestApiBaseUrl": "http://localhost:5001",
    "HealthCheckTimeoutSeconds": 30,
    "DefaultRequestTimeoutSeconds": 10
  },
  "Docker": {
    "ContainerName": "slipka_integration_test"
  }
}
```

## Test Categories

### 1. Proxy Management
- **Dynamic Proxy Creation**: Tests creating proxies with various configurations
- **Static Proxy Management**: Tests static proxy configuration and lifecycle
- **Proxy Deletion**: Tests proper cleanup of proxy instances

### 2. Response Injection
- **Basic Injection**: Tests injecting responses for specific requests
- **Conditional Injection**: Tests injection based on headers, methods, URIs
- **Injection Priority**: Tests precedence when multiple injections match

### 3. Traffic Recording
- **Request Recording**: Tests capturing and storing HTTP requests
- **Response Recording**: Tests capturing and storing HTTP responses
- **Recording Filters**: Tests selective recording based on criteria

### 4. Request Decoration
- **Header Addition**: Tests adding headers to proxied requests
- **Multiple Decorations**: Tests applying multiple decorations
- **Decoration Precedence**: Tests header precedence rules

### 5. Preprocessors
- **Header Authentication**: Tests automatic header injection
- **Session-Based Auth**: Tests OAuth session management
- **Custom Preprocessors**: Tests user-defined preprocessor logic

### 6. Call Tagging
- **Automatic Tagging**: Tests tagging based on matching rules
- **Tag Filtering**: Tests retrieving calls by tags
- **Performance Tagging**: Tests duration-based tagging

### 7. Session API
- **Session Retrieval**: Tests getting complete session data
- **Call Filtering**: Tests filtering calls by various criteria
- **Data Export**: Tests exporting session data for analysis

### 8. Health Monitoring
- **Service Health**: Tests overall service health checks
- **Static Proxy Health**: Tests individual proxy health monitoring
- **Health Degradation**: Tests health check responses under load

### 9. Error Scenarios
- **Invalid Configurations**: Tests handling of invalid proxy configs
- **Network Failures**: Tests behavior when target services are unreachable
- **Resource Limits**: Tests behavior under resource constraints

## Test API Server

The integration tests include a Test API server (`TestApi`) that provides endpoints for testing proxy behavior:

- `GET /api/test/echo` - Echoes back request data
- `GET /api/test/status/{code}` - Returns specific HTTP status codes
- `POST /api/test/echo` - Echoes POST request bodies
- `GET /api/test/slow/{delay}` - Simulates slow responses
- `GET /api/test/user/{id}` - Tests user retrieval scenarios
- `POST /api/test/data` - Tests data processing with validation

## Writing New Tests

1. **Inherit from IntegrationTestBase**: All tests should inherit from `IntegrationTestBase`
2. **Use descriptive test names**: Follow the pattern `TestFeature_Scenario_ExpectedResult`
3. **Clean up resources**: The base class handles most cleanup, but override `CleanupTestDataAsync` if needed
4. **Use assertions**: Leverage FluentAssertions for readable assertions
5. **Handle async operations**: Use `async`/`await` for all HTTP operations

Example:

```csharp
public class MyFeatureTests : IntegrationTestBase
{
    public MyFeatureTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateProxy_WithValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = new { targetHost = "example.com", targetPort = 80 };

        // Act
        var proxy = await CreateProxyAsync("example.com", 80);

        // Assert
        proxy.Should().NotBeNull();
        proxy.ProxyPort.Should().BeGreaterThan(61000);
        proxy.TargetHost.Should().Be("example.com");
    }
}
```

## CI/CD Integration

The integration tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Integration Tests
  run: |
    docker-compose up -d
    cd integrationTests
    dotnet test --logger "trx;LogFileName=test-results.trx"
```

## Troubleshooting

### Common Issues

1. **Slipka service not healthy**:
   - Check Docker containers: `docker-compose ps`
   - Check logs: `docker-compose logs slipka`
   - Wait longer for startup: services may take time to initialize

2. **Port conflicts**:
   - Ensure ports 4445, 5001, 27017, 6379 are available
   - Check for other running services using these ports

3. **Test timeouts**:
   - Increase timeout values in `appsettings.json`
   - Check network connectivity to target services
   - Verify Docker networking is working correctly

4. **Database connection issues**:
   - Ensure MongoDB and Redis are running
   - Check connection strings in Slipka configuration

### Debug Mode

Run tests in debug mode to see detailed output:

```bash
# Set environment variable for debug logging
export ASPNETCORE_ENVIRONMENT=Development

# Run with verbose output
dotnet test --logger "console;verbosity=detailed" --filter "FullyQualifiedName~MyTest"
```

## Contributing

When adding new tests:

1. Follow the existing directory structure
2. Add appropriate test categories
3. Include both positive and negative test cases
4. Document complex test scenarios
5. Update this README if adding new test categories
