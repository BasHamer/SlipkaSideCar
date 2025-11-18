# Slipka Integration Test Plan

## Overview

This document outlines a comprehensive integration test suite for the Slipka proxy service. The tests are designed as black-box integration tests that validate all major Slipka features end-to-end without referencing internal DLLs.

## Architecture

### Test Structure
```
integrationTests/
├── Slipka.IntegrationTests.sln                    # Solution file
├── Slipka.IntegrationTests/                       # Main test project
│   ├── appsettings.json                          # Test configuration
│   ├── Fixtures/                                 # Test infrastructure
│   │   ├── IntegrationTestBase.cs               # Base test class
│   │   ├── TestConfiguration.cs                 # Configuration management
│   │   └── TestApiFixture.cs                    # Test API server lifecycle
│   ├── TestApi/                                 # Test target API server
│   │   ├── Controllers/TestController.cs        # Test endpoints
│   │   ├── Program.cs                           # API server entry point
│   │   ├── Dockerfile                          # Container definition
│   │   └── appsettings.json                     # API configuration
│   └── Tests/                                   # Test implementations
│       ├── ProxyManagement/                     # Dynamic & static proxy tests
│       ├── ResponseInjection/                   # Injection feature tests
│       ├── TrafficRecording/                    # Recording feature tests
│       ├── RequestDecoration/                   # Decoration feature tests
│       ├── Preprocessors/                       # Authentication tests
│       ├── CallTagging/                         # Tagging feature tests
│       ├── SessionApi/                          # Data retrieval tests
│       ├── HealthMonitoring/                    # Health check tests
│       └── ErrorScenarios/                      # Error handling tests
├── docker-compose.integration.yml               # Test environment setup
├── .github/workflows/integration-tests.yml      # CI/CD pipeline
└── README.md                                    # Usage documentation
```

### Test API Server

The integration tests include a dedicated Test API server that provides predictable endpoints for testing:

- **GET /api/test/echo** - Echoes request data and headers
- **POST /api/test/echo** - Echoes POST request bodies
- **GET /api/test/status/{code}** - Returns specific HTTP status codes
- **GET /api/test/slow/{delayMs}** - Simulates slow responses
- **GET /api/test/user/{id}** - Tests user retrieval scenarios
- **POST /api/test/data** - Tests data processing with validation
- **GET /api/test/headers** - Tests header inspection
- **GET /api/test/large** - Tests large response handling
- **GET /api/test/auth-required** - Tests authentication scenarios

## Test Coverage

### 1. Dynamic Proxy Management ✅ IMPLEMENTED
- **Proxy Creation**: Tests creating proxies with various configurations
- **Proxy Accessibility**: Verifies proxies route traffic correctly
- **Port Assignment**: Ensures unique port allocation
- **Timing Configuration**: Tests custom open/retain durations
- **Proxy Deletion**: Verifies proper cleanup
- **Error Handling**: Tests invalid configuration scenarios

### 2. Response Injection ✅ IMPLEMENTED
- **Basic Injection**: Tests replacing responses for specific URIs
- **Regex Matching**: Tests pattern-based URI matching
- **Method Filtering**: Tests HTTP method-specific injection
- **Header Filtering**: Tests injection based on request headers
- **Delay Simulation**: Tests response delays for performance testing
- **Status Code Injection**: Tests custom HTTP status codes
- **Multiple Injections**: Tests injection precedence rules
- **Tagged Injections**: Tests injection with metadata tags

### 3. Traffic Recording ✅ IMPLEMENTED
- **Request Capture**: Tests recording of HTTP requests
- **Response Capture**: Tests recording of HTTP responses
- **Body Storage**: Tests request/response body persistence
- **URI Filtering**: Tests selective recording by URI patterns
- **Header Filtering**: Tests recording based on request headers
- **Duration Thresholds**: Tests recording based on response times
- **Multiple Rules**: Tests combining multiple recording criteria
- **Data Retrieval**: Tests accessing recorded session data

### 4. Request Decoration ⏳ PENDING
- **Header Addition**: Tests adding headers to proxied requests
- **Multiple Headers**: Tests applying multiple decorations
- **Header Precedence**: Tests header override behavior
- **Conditional Decoration**: Tests decoration based on request criteria

### 5. Built-in Preprocessors ⏳ PENDING
- **Header Authentication**: Tests automatic header injection for auth
- **Session-Based Auth**: Tests OAuth session management
- **Token Refresh**: Tests automatic token renewal
- **Authentication Errors**: Tests auth failure scenarios

### 6. Call Tagging ⏳ PENDING
- **Automatic Tagging**: Tests rule-based call tagging
- **Tag Filtering**: Tests retrieving calls by tags
- **Performance Tagging**: Tests duration-based tagging
- **Custom Tags**: Tests user-defined tagging rules

### 7. Session API Endpoints ⏳ PENDING
- **Session Retrieval**: Tests getting complete session data
- **Call Filtering**: Tests filtering by various criteria (tags, duration, status)
- **Pagination**: Tests large dataset handling
- **Data Export**: Tests session data export functionality

### 8. Health Monitoring ⏳ PENDING
- **Service Health**: Tests overall service health checks
- **Static Proxy Health**: Tests individual proxy monitoring
- **Health Degradation**: Tests health check responses under load
- **Health Recovery**: Tests health status after failures

### 9. Error Scenarios ⏳ PENDING
- **Invalid Configurations**: Tests handling of malformed requests
- **Network Failures**: Tests behavior when targets are unreachable
- **Resource Limits**: Tests behavior under memory/CPU constraints
- **Timeout Handling**: Tests request timeout scenarios
- **Connection Errors**: Tests network connectivity issues

## Implementation Status

### ✅ Completed Features
- Solution structure and project setup
- Test framework configuration (xUnit, FluentAssertions, RestSharp)
- Base test infrastructure with Docker health checks
- Test API server with comprehensive endpoints
- Dynamic proxy creation and management tests
- Response injection with advanced matching rules
- Traffic recording with filtering and data retrieval
- Docker Compose configuration for test environment
- CI/CD pipeline configuration

### ⏳ Remaining Features
- Request decoration tests
- Built-in preprocessor tests (authentication)
- Call tagging functionality tests
- Session API data retrieval tests
- Health monitoring endpoint tests
- Comprehensive error scenario tests

## Test Execution

### Prerequisites
1. Docker and Docker Compose installed
2. .NET 8.0 SDK installed
3. Ports 4445, 5001, 27017, 6379 available

### Running Tests Locally

```bash
# 1. Start the test environment
cd integrationTests
docker-compose -f ../src/docker-compose.yml -f docker-compose.integration.yml up -d

# 2. Wait for services to be healthy (or run the health check script)
# Services will be ready when health checks pass

# 3. Run the tests
dotnet test

# 4. View detailed results
dotnet test --logger "console;verbosity=detailed"

# 5. Clean up
docker-compose down -v
```

### CI/CD Execution
The tests are configured to run automatically in GitHub Actions on pushes and pull requests to main/develop branches.

## Test Design Principles

### Black-Box Testing
- Tests treat Slipka as a black box
- No references to internal assemblies
- Validation through HTTP APIs only
- Focus on externally observable behavior

### Isolation
- Each test creates its own proxy instance
- Automatic cleanup prevents test interference
- Test API server provides consistent target behavior
- Docker isolation ensures clean environment

### Resilience
- Retry logic for transient failures
- Health checks before test execution
- Graceful handling of service unavailability
- Timeout management for long-running operations

### Comprehensive Coverage
- Positive test cases (expected behavior)
- Negative test cases (error conditions)
- Edge cases and boundary conditions
- Performance and timing scenarios

## Extending the Test Suite

### Adding New Test Categories

1. Create a new directory under `Tests/`
2. Create test classes inheriting from `IntegrationTestBase`
3. Implement test methods following the naming convention
4. Add appropriate cleanup in test teardown

### Adding Test API Endpoints

1. Add new methods to `TestController.cs`
2. Update the Dockerfile if additional dependencies are needed
3. Document the new endpoints in this plan

### Modifying Test Configuration

1. Update `appsettings.json` for test project configuration
2. Update `docker-compose.integration.yml` for environment changes
3. Update CI/CD pipeline if execution requirements change

## Troubleshooting

### Common Issues

**Services not healthy**: Check Docker container logs with `docker-compose logs`
**Port conflicts**: Ensure required ports are available
**Test timeouts**: Increase timeout values in test configuration
**Database connection issues**: Verify MongoDB and Redis connectivity

### Debug Mode

Run individual tests with detailed logging:
```bash
dotnet test --filter "FullyQualifiedName~SpecificTest" --logger "console;verbosity=detailed"
```

## Future Enhancements

### Performance Testing
- Load testing with multiple concurrent proxies
- Memory usage monitoring during extended runs
- Database performance under high traffic

### UI Testing
- Selenium-based tests for web interface (if implemented)
- Visual regression testing
- Accessibility testing

### Chaos Engineering
- Network partition testing
- Service dependency failure simulation
- Resource exhaustion testing

### Multi-Environment Testing
- Testing against different target services
- Cross-platform compatibility testing
- Cloud deployment validation
