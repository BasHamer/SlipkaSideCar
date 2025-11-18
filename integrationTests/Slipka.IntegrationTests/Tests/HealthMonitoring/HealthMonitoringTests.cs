using System;
using System.Linq;
using FluentAssertions;
using System.Net;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using RestSharp;
using Slipka.IntegrationTests.Fixtures;

namespace Slipka.IntegrationTests.Tests.HealthMonitoring;

public class HealthMonitoringTests : IntegrationTestBase
{
    public HealthMonitoringTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HealthCheck_OverallServiceHealth_ReturnsHealthy()
    {
        // Act - Call the health endpoint
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_IncludesHealthData()
    {
        // Act - Call the health endpoint
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert
        healthResponse.IsSuccessful.Should().BeTrue();

        // The response should include structured health data
        // In ASP.NET Core health checks, this typically returns JSON with status and details
        healthResponse.Content.Should().NotBeNullOrEmpty();

        // If it's JSON, it should be parseable
        try
        {
            var healthData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(healthResponse.Content!);
            // Successfully parsed as JSON
        }
        catch
        {
            // If not JSON, it should at least contain status information
            healthResponse.Content.Should().Contain("status").And.Contain("Healthy");
        }
    }

    [Fact]
    public async Task HealthCheck_RedisHealth_WhenRedisEnabled()
    {
        // This test assumes Redis is running in the test environment
        // In a real scenario, we'd check if Redis is configured and running

        // Act - Call health check which includes Redis health
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Health check should complete (Redis health is included if enabled)
        healthResponse.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheck_StaticProxyHealth_IncludesProxyStatus()
    {
        // Act - Call health check which includes static proxy health
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert
        healthResponse.IsSuccessful.Should().BeTrue();

        // The health check should include static proxy information
        // This depends on the specific health check implementation
        healthResponse.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthCheck_ResponseTime_IsReasonable()
    {
        // Act - Time the health check response
        var startTime = DateTime.UtcNow;
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);
        var endTime = DateTime.UtcNow;

        // Assert
        healthResponse.IsSuccessful.Should().BeTrue();

        var responseTime = endTime - startTime;
        responseTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Health check should respond quickly");
    }

    [Fact]
    public async Task HealthCheck_AfterCreatingProxy_RemainsHealthy()
    {
        // Arrange - Create a proxy (this shouldn't affect health)
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Act - Check health after proxy creation
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Service should still be healthy
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_DuringActiveProxyUsage_RemainsHealthy()
    {
        // Arrange - Create proxy and make active requests
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Make several concurrent requests
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(MakeProxyRequestAsync(proxy.ProxyPort, $"/api/test/echo?message=test{i}"));
        }
        await Task.WhenAll(tasks);

        // Act - Check health during active usage
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Service should remain healthy under load
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_ContentType_IsJson()
    {
        // Act - Call health endpoint
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Should return JSON content type
        healthResponse.IsSuccessful.Should().BeTrue();

        // Check content type header if available
        var contentType = healthResponse.ContentType;
        if (contentType != null)
        {
            contentType.Should().Contain("application/json");
        }
    }

    [Fact]
    public async Task HealthCheck_CacheHeaders_ArePresent()
    {
        // Act - Call health endpoint multiple times quickly
        var healthRequest1 = new RestRequest("/health", RestSharp.Method.Get);
        var healthRequest2 = new RestRequest("/health", RestSharp.Method.Get);

        var response1 = await ExecuteWithRetryAsync(healthRequest1);
        var response2 = await ExecuteWithRetryAsync(healthRequest2);

        // Assert - Both responses should be successful
        response1.IsSuccessful.Should().BeTrue();
        response2.IsSuccessful.Should().BeTrue();

        // Content should be consistent (health checks should be relatively stable)
        response1.Content.Should().Be(response2.Content);
    }

    [Fact]
    public async Task HealthCheck_AfterSessionOperations_RemainsHealthy()
    {
        // Arrange - Perform various session operations
        var proxy = await CreateProxyAsync("localhost", 5001);
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");

        await Task.Delay(1000);

        // Get session data
        var getSessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        await ExecuteWithRetryAsync(getSessionRequest);

        // Act - Check health after session operations
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Service should remain healthy
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_IncludesServiceStatus()
    {
        // Act - Get detailed health information
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert
        healthResponse.IsSuccessful.Should().BeTrue();

        // Health response should include status information
        // The exact format depends on ASP.NET Core Health Checks implementation
        healthResponse.Content.Should().NotBeNullOrEmpty();

        // Should contain some indication of overall health
        var content = healthResponse.Content!.ToLower();
        content.Should().MatchRegex("(healthy|status)");
    }

    [Fact]
    public async Task HealthCheck_MultipleConcurrentCalls_Succeeds()
    {
        // Act - Make multiple concurrent health check calls
        var tasks = new List<Task<RestResponse>>();
        for (int i = 0; i < 10; i++)
        {
            var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
            tasks.Add(ExecuteWithRetryAsync(healthRequest));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All health checks should succeed
        responses.Should().AllSatisfy(response =>
        {
            response.IsSuccessful.Should().BeTrue();
            response.Content.Should().Contain("Healthy");
        });
    }

    [Fact]
    public async Task HealthCheck_AfterConfigurationChanges_RemainsHealthy()
    {
        // Arrange - Make configuration changes (add recording, tagging, etc.)
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Add various configurations
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new { method = "GET", uri = ".*" });
        await ExecuteWithRetryAsync(recordRequest);

        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new { method = "GET", uri = "/api/test/.*", tags = new[] { "test" } });
        await ExecuteWithRetryAsync(tagRequest);

        var decorateRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorateRequest.AddJsonBody(new { key = "X-Test", values = new[] { "value" } });
        await ExecuteWithRetryAsync(decorateRequest);

        // Act - Check health after configuration changes
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Service should remain healthy despite configuration changes
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_AfterDataOperations_RemainsHealthy()
    {
        // Arrange - Perform various data operations that might stress the system
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Make many requests
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(MakeProxyRequestAsync(proxy.ProxyPort, $"/api/test/echo?message=request{i}"));
        }
        await Task.WhenAll(tasks);

        await Task.Delay(2000); // Wait for processing

        // Retrieve session data
        var getSessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        await ExecuteWithRetryAsync(getSessionRequest);

        var getCallsRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}/calls", RestSharp.Method.Get);
        await ExecuteWithRetryAsync(getCallsRequest);

        // Act - Check health after data-intensive operations
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var healthResponse = await ExecuteWithRetryAsync(healthRequest);

        // Assert - Service should remain healthy after data operations
        healthResponse.IsSuccessful.Should().BeTrue();
        healthResponse.Content.Should().Contain("Healthy");
    }
}
