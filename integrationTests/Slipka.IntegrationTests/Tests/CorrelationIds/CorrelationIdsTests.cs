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

namespace Slipka.IntegrationTests.Tests.CorrelationIds;

public class CorrelationIdsTests : IntegrationTestBase
{
    public CorrelationIdsTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CorrelationIdGeneration_ProxyGeneratesCorrelationId_WhenNotProvided()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request without correlation ID headers
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation");

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().NotBeNullOrEmpty();
        result.CorrelationId.Should().NotBe("not-provided");
        result.CorrelationSubId.Should().NotBeNullOrEmpty();
        result.CorrelationSubId.Should().NotBe("not-provided");
        // Correlation IDs should be GUIDs
        Guid.TryParse(result.CorrelationId, out _).Should().BeTrue();
        Guid.TryParse(result.CorrelationSubId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationIdPropagation_ProxyPropagatesExistingCorrelationId()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var expectedCorrelationSubId = Guid.NewGuid().ToString();

        // Act - Make request with correlation ID headers
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = expectedCorrelationId,
            ["x-correlation-sub-id"] = expectedCorrelationSubId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().Be(expectedCorrelationId);
        result.CorrelationSubId.Should().Be(expectedCorrelationSubId);
    }

    [Fact]
    public async Task CorrelationIdPropagation_ProxyGeneratesSubId_WhenOnlyMainIdProvided()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var expectedCorrelationId = Guid.NewGuid().ToString();

        // Act - Make request with only correlation ID header
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = expectedCorrelationId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().Be(expectedCorrelationId);
        result.CorrelationSubId.Should().NotBeNullOrEmpty();
        result.CorrelationSubId.Should().NotBe("not-provided");
        Guid.TryParse(result.CorrelationSubId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationIdPropagation_ProxyGeneratesMainId_WhenOnlySubIdProvided()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var expectedCorrelationSubId = Guid.NewGuid().ToString();

        // Act - Make request with only correlation sub-ID header
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-sub-id"] = expectedCorrelationSubId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().NotBeNullOrEmpty();
        result.CorrelationId.Should().NotBe("not-provided");
        Guid.TryParse(result.CorrelationId, out _).Should().BeTrue();
        result.CorrelationSubId.Should().Be(expectedCorrelationSubId);
    }

    [Fact]
    public async Task CrossRequestCorrelation_SameCorrelationIdAcrossMultipleRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var correlationId = Guid.NewGuid().ToString();

        // Act - Make multiple requests with the same correlation ID
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId
        };

        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);
        var response3 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);
        AssertSuccessStatusCode(response3);

        var result1 = await DeserializeResponseAsync<CorrelationResponse>(response1);
        var result2 = await DeserializeResponseAsync<CorrelationResponse>(response2);
        var result3 = await DeserializeResponseAsync<CorrelationResponse>(response3);

        // All requests should have the same correlation ID
        result1.CorrelationId.Should().Be(correlationId);
        result2.CorrelationId.Should().Be(correlationId);
        result3.CorrelationId.Should().Be(correlationId);

        // Each request should have different sub-IDs
        var subIds = new[] { result1.CorrelationSubId, result2.CorrelationSubId, result3.CorrelationSubId };
        subIds.Should().OnlyHaveUniqueItems();
        subIds.All(subId => Guid.TryParse(subId, out _)).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationIdValidation_AcceptsValidGuidFormat()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var validCorrelationId = Guid.NewGuid().ToString();

        // Act
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = validCorrelationId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().Be(validCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdValidation_AcceptsCustomFormatIds()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var customCorrelationId = "custom-request-12345";

        // Act
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = customCorrelationId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().Be(customCorrelationId);
    }

    [Fact]
    public async Task IdCollisionHandling_HandlesDuplicateCorrelationIdsGracefully()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var duplicateCorrelationId = Guid.NewGuid().ToString();

        // Act - Make multiple requests with the same correlation ID simultaneously
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = duplicateCorrelationId
        };

        var task1 = MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);
        var task2 = MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);
        var task3 = MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        await Task.WhenAll(task1, task2, task3);

        // Assert - All requests should succeed
        AssertSuccessStatusCode(task1.Result);
        AssertSuccessStatusCode(task2.Result);
        AssertSuccessStatusCode(task3.Result);

        var result1 = await DeserializeResponseAsync<CorrelationResponse>(task1.Result);
        var result2 = await DeserializeResponseAsync<CorrelationResponse>(task2.Result);
        var result3 = await DeserializeResponseAsync<CorrelationResponse>(task3.Result);

        // All should have the same main correlation ID
        result1.CorrelationId.Should().Be(duplicateCorrelationId);
        result2.CorrelationId.Should().Be(duplicateCorrelationId);
        result3.CorrelationId.Should().Be(duplicateCorrelationId);

        // But different sub-IDs
        var subIds = new[] { result1.CorrelationSubId, result2.CorrelationSubId, result3.CorrelationSubId };
        subIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DistributedTracing_CorrelationIdsPropagateThroughMultipleProxies()
    {
        // Arrange - Create two proxies to simulate distributed scenario
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy1.ProxyPort);
        await WaitForProxyReadyAsync(proxy2.ProxyPort);

        var correlationId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId
        };

        // Act - Make request through first proxy
        var response1 = await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/correlation", headers: headers);
        var result1 = await DeserializeResponseAsync<CorrelationResponse>(response1);

        // Make another request through second proxy with same correlation ID
        var response2 = await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/correlation", headers: headers);
        var result2 = await DeserializeResponseAsync<CorrelationResponse>(response2);

        // Assert
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        // Both should have the same correlation ID
        result1.CorrelationId.Should().Be(correlationId);
        result2.CorrelationId.Should().Be(correlationId);

        // But different sub-IDs (since they're different proxy sessions)
        result1.CorrelationSubId.Should().NotBe(result2.CorrelationSubId);
    }

    [Fact]
    public async Task LoggingIntegration_CorrelationIdsIncludedInLogMessages()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var correlationSubId = Guid.NewGuid().ToString();

        var logMessage = new
        {
            correlationId = correlationId,
            originalCorrelationSubId = correlationSubId,
            newCorrelationSubId = Guid.NewGuid().ToString(),
            level = "Information",
            message = "Test log message for correlation ID integration",
            category = "IntegrationTest",
            properties = new Dictionary<string, object>
            {
                ["testProperty"] = "testValue",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }
        };

        // Act - Submit log message via logging API
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.Message.Should().Be("Log entry recorded");
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LoggingIntegration_InvalidCorrelationId_Rejected()
    {
        // Arrange - Invalid correlation ID (contains XSS-like content)
        var invalidLogMessage = new
        {
            correlationId = "<script>alert('xss')</script>",
            level = "Information",
            message = "Test log message with invalid correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(invalidLogMessage);

        // Assert - Should get BadRequest due to validation
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task LoggingIntegration_LogLevelsProcessedCorrectly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var testCases = new[]
        {
            ("Debug", "Debug level test"),
            ("Information", "Info level test"),
            ("Warning", "Warning level test"),
            ("Error", "Error level test"),
            ("Critical", "Critical level test")
        };

        // Act & Assert - Test each log level
        foreach (var (level, message) in testCases)
        {
            var logMessage = new
            {
                correlationId = correlationId,
                level = level,
                message = message
            };

            var request = new RestRequest("/api/logging", RestSharp.Method.Post);
            request.AddJsonBody(logMessage);

            var response = await ExecuteWithRetryAsync(request);
            AssertSuccessStatusCode(response);
        }
    }

    [Fact]
    public async Task LoggingIntegration_StructuredLoggingWithProperties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var properties = new Dictionary<string, object>
        {
            ["userId"] = "12345",
            ["action"] = "login",
            ["ipAddress"] = "192.168.1.100",
            ["userAgent"] = "IntegrationTest/1.0"
        };

        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "User login attempt",
            category = "Authentication",
            properties = properties
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LoggingIntegration_ExceptionLogging()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var exceptionDetails = "System.NullReferenceException: Object reference not set to an instance of an object";

        var logMessage = new
        {
            correlationId = correlationId,
            level = "Error",
            message = "An unexpected error occurred during processing",
            exception = exceptionDetails,
            category = "ErrorHandling"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CorrelationIdTracking_ReverseProxyScenario()
    {
        // This test would require reverse proxy setup, which might be complex for integration tests
        // For now, we'll test that correlation headers are properly forwarded

        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var correlationId = Guid.NewGuid().ToString();
        var correlationSubId = Guid.NewGuid().ToString();

        // Act - Make request that would typically go through reverse proxy
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId,
            ["x-correlation-sub-id"] = correlationSubId,
            ["X-Forwarded-For"] = "203.0.113.1",
            ["X-Forwarded-Proto"] = "https"
        };

        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/correlation", headers: headers);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<CorrelationResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
        result.CorrelationSubId.Should().Be(correlationSubId);

        // Verify all headers were received
        result.AllHeaders.Should().ContainKey("x-correlation-id");
        result.AllHeaders.Should().ContainKey("x-correlation-sub-id");
        result.AllHeaders["x-correlation-id"].Should().Be(correlationId);
        result.AllHeaders["x-correlation-sub-id"].Should().Be(correlationSubId);
    }
}

public class CorrelationResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string CorrelationSubId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> AllHeaders { get; set; } = new();
}

public class LogResponse
{
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
