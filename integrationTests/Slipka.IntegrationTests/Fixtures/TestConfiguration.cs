using System.IO;
using Microsoft.Extensions.Configuration;

namespace Slipka.IntegrationTests.Fixtures;

public class TestConfiguration
{
    private readonly IConfiguration _configuration;

    public TestConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public string SlipkaBaseUrl => _configuration["Slipka:BaseUrl"] ?? "http://localhost:4445";
    public string TestApiBaseUrl => _configuration["Slipka:TestApiBaseUrl"] ?? "http://localhost:5001";
    public int HealthCheckTimeoutSeconds => int.Parse(_configuration["Slipka:HealthCheckTimeoutSeconds"] ?? "30");
    public int DefaultRequestTimeoutSeconds => int.Parse(_configuration["Slipka:DefaultRequestTimeoutSeconds"] ?? "10");

    public string DockerContainerName => _configuration["Docker:ContainerName"] ?? "slipka_integration_test";
    public string DockerImageName => _configuration["Docker:ImageName"] ?? "slipka:latest";
    public string MongoContainerName => _configuration["Docker:MongoContainerName"] ?? "slipka_integration_mongo";
    public string RedisContainerName => _configuration["Docker:RedisContainerName"] ?? "slipka_integration_redis";

    public string TestUserId => _configuration["TestData:TestUserId"] ?? "test-user-123";
    public string TestApiKey => _configuration["TestData:TestApiKey"] ?? "test-api-key-456";
    public string TestSessionId => _configuration["TestData:TestSessionId"] ?? "integration-test-session";
}
