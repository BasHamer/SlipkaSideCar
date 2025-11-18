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

namespace Slipka.IntegrationTests.Tests.ProxyManagement;

public class DynamicProxyTests : IntegrationTestBase
{
    public DynamicProxyTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateProxy_WithValidConfig_ReturnsSuccess()
    {
        // Arrange & Act
        var proxy = await CreateProxyAsync("httpbin.org", 80);

        // Assert
        proxy.Should().NotBeNull();
        proxy.Id.Should().NotBeNullOrEmpty();
        proxy.ProxyPort.Should().BeGreaterThan(61000).And.BeLessThan(62000);
        proxy.TargetHost.Should().Be("httpbin.org");
        proxy.TargetPort.Should().Be(80);
        proxy.LeaveProxyOpenUntil.Should().BeAfter(DateTime.UtcNow);
        proxy.RetainDataUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateProxy_WithCustomTiming_ReturnsConfiguredTiming()
    {
        // Arrange
        var openFor = "00:10:00"; // 10 minutes
        var retainedFor = "01:00:00"; // 1 hour

        // Act
        var proxy = await CreateProxyAsync("httpbin.org", 80, openFor, retainedFor);

        // Assert
        proxy.Should().NotBeNull();
        var expectedOpenUntil = DateTime.UtcNow.AddMinutes(10);
        var expectedRetainUntil = DateTime.UtcNow.AddHours(1);

        proxy.LeaveProxyOpenUntil.Should().BeCloseTo(expectedOpenUntil, TimeSpan.FromMinutes(1));
        proxy.RetainDataUntil.Should().BeCloseTo(expectedRetainUntil, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task CreateProxy_ThenProxyIsAccessible_ReturnsSuccess()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 80);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/status/200");

        // Assert
        AssertSuccessStatusCode(response);
    }

    [Fact]
    public async Task CreateProxy_WithHttpsTarget_ProxiesHttpsRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to HTTPS endpoint through proxy
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/json");

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("slideshow");
    }

    [Fact]
    public async Task CreateProxy_ThenDeleteProxy_RemovesProxySuccessfully()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 80);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Verify proxy exists by making a request
        var beforeDeleteResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/status/200");
        AssertSuccessStatusCode(beforeDeleteResponse);

        // Act
        await DeleteProxyAsync(proxy.Id);

        // Assert - Proxy should no longer be accessible
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await MakeProxyRequestAsync(proxy.ProxyPort, "/status/200");
        });
    }

    [Fact]
    public async Task CreateMultipleProxies_AssignsUniquePorts()
    {
        // Arrange & Act
        var proxy1 = await CreateProxyAsync("httpbin.org", 80);
        var proxy2 = await CreateProxyAsync("httpbin.org", 80);
        var proxy3 = await CreateProxyAsync("httpbin.org", 80);

        // Assert
        var ports = new[] { proxy1.ProxyPort, proxy2.ProxyPort, proxy3.ProxyPort };
        ports.Should().OnlyHaveUniqueItems();
        ports.Should().AllSatisfy(port => port.Should().BeInRange(61710, 61920));
    }

    [Fact]
    public async Task CreateProxy_WithLocalhostTarget_ProxiesToTestApi()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=integration-test");

        // Assert
        AssertSuccessStatusCode(response);
        var result = await DeserializeResponseAsync<EchoResponse>(response);
        result.Message.Should().Be("integration-test");
        result.Timestamp.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        result.Timestamp.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CreateProxy_WithPostRequest_ProxiesPostBodyCorrectly()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var requestBody = new
        {
            message = "POST integration test",
            number = 42,
            metadata = new Dictionary<string, string>
            {
                ["test"] = "value",
                ["source"] = "integration"
            }
        };

        // Act
        var response = await MakeProxyRequestAsync(
            proxy.ProxyPort,
            "/api/test/echo",
            HttpMethod.Post,
            requestBody);

        // Assert
        AssertSuccessStatusCode(response);
        var result = await DeserializeResponseAsync<EchoResponse>(response);
        result.Received.Should().NotBeNull();
        var received = (EchoRequest)result.Received;
        received.Message.Should().Be("POST integration test");
        received.Number.Should().Be(42);
        received.Metadata.Should().ContainKey("test").WhoseValue.Should().Be("value");
        received.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("integration");
    }

    [Fact]
    public async Task CreateProxy_WithInvalidTargetHost_ReturnsError()
    {
        // Arrange - Try to create proxy with invalid hostname
        var request = new RestRequest("/api/Proxies", RestSharp.Method.Post);
        request.AddJsonBody(new
        {
            targetHost = "", // Invalid empty hostname
            targetPort = 80
        });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }
}

public class SimpleProxyTest
{
    [Fact]
    public async Task SimplestProxyTest_CanReachSlipkaAPI()
    {
        // Simple test that just checks if we can reach the Slipka API
        var client = new RestClient("http://localhost:4445");
        var request = new RestRequest("/health", RestSharp.Method.Get);

        var response = await client.ExecuteAsync(request);

        response.IsSuccessful.Should().BeTrue();
        response.Content.Should().Contain("Healthy");
    }
}

public class EchoRequest
{
    public string Message { get; set; } = string.Empty;
    public int Number { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class EchoResponse
{
    public string Message { get; set; } = string.Empty;
    public int Number { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public object Received { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
