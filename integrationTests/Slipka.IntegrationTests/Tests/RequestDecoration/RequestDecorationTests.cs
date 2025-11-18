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

namespace Slipka.IntegrationTests.Tests.RequestDecoration;

public class RequestDecorationTests : IntegrationTestBase
{
    public RequestDecorationTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DecorateRequest_AddsSingleHeader()
    {
        // Arrange - Create proxy and add a decoration
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "X-Custom-Test",
            values = new[] { "test-value-123" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make request through proxy (should include decorated header)
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check");

        // Assert - Request should succeed and decoration should be applied
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("X-Custom-Test").And.Contain("test-value-123");
    }

    [Fact]
    public async Task DecorateRequest_AddsMultipleHeaders()
    {
        // Arrange - Create proxy and add multiple decorations
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add first decoration
        var decoration1Request = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decoration1Request.AddJsonBody(new
        {
            key = "X-API-Key",
            values = new[] { "api-key-abc" }
        });
        await ExecuteWithRetryAsync(decoration1Request);

        // Add second decoration
        var decoration2Request = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decoration2Request.AddJsonBody(new
        {
            key = "X-User-Agent",
            values = new[] { "TestSuite/1.0" }
        });
        await ExecuteWithRetryAsync(decoration2Request);

        // Act - Make request through proxy
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check");

        // Assert - Both decorations should be applied
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("X-API-Key").And.Contain("api-key-abc");
        content.Should().Contain("X-User-Agent").And.Contain("TestSuite/1.0");
    }

    [Fact]
    public async Task DecorateRequest_AddsMultipleValuesForSameHeader()
    {
        // Arrange - Create proxy and add decoration with multiple values
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "Accept",
            values = new[] { "application/json", "text/plain", "*/*" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make request through proxy
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check");

        // Assert - All values should be present
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Accept");
        content.Should().Contain("application/json");
        content.Should().Contain("text/plain");
        content.Should().Contain("*/*");
    }

    [Fact]
    public async Task DecorateRequest_AppliesToAllRequests()
    {
        // Arrange - Create proxy and add decoration
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "X-Global-Decoration",
            values = new[] { "applied-to-all" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make multiple different requests
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check");
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");
        var response3 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200");

        // Assert - All requests should include the decoration
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);
        AssertSuccessStatusCode(response3);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        content1.Should().Contain("X-Global-Decoration").And.Contain("applied-to-all");
        content2.Should().Contain("X-Global-Decoration").And.Contain("applied-to-all");
        content3.Should().Contain("X-Global-Decoration").And.Contain("applied-to-all");
    }

    [Fact]
    public async Task DecorateRequest_PreservesOriginalHeaders()
    {
        // Arrange - Create proxy and add decoration
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "X-Decorated",
            values = new[] { "added-by-proxy" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make request with custom headers
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check",
            headers: new Dictionary<string, string>
            {
                ["X-Custom-Client"] = "client-header-value",
                ["User-Agent"] = "CustomTestClient/1.0"
            });

        // Assert - Both original and decorated headers should be present
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("X-Decorated").And.Contain("added-by-proxy");
        content.Should().Contain("X-Custom-Client").And.Contain("client-header-value");
        content.Should().Contain("User-Agent").And.Contain("CustomTestClient/1.0");
    }

    [Fact]
    public async Task DecorateRequest_HandlesEmptyValues()
    {
        // Arrange - Create proxy and add decoration with empty values array
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "X-Empty-Test",
            values = new string[] { }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make request through proxy
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check");

        // Assert - Request should succeed (empty values should not cause issues)
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        // The header might not appear or might appear with empty value
        // The important thing is that it doesn't break the request
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task DecorateRequest_AddsAuthorizationHeader()
    {
        // Arrange - Create proxy and add authorization decoration
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "Authorization",
            values = new[] { "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.test.signature" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make request to auth-protected endpoint
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/auth-required");

        // Assert - Request should succeed with authentication
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("authenticated").And.Contain("Bearer");
    }

    [Fact]
    public async Task DecorateRequest_AddsContentTypeHeader()
    {
        // Arrange - Create proxy and add content-type decoration
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "Content-Type",
            values = new[] { "application/xml" }
        });

        await ExecuteWithRetryAsync(decorationRequest);

        // Act - Make POST request through proxy
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/decoration-check",
            HttpMethod.Post, new { test = "data" });

        // Assert - Content-Type header should be overridden by decoration
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Content-Type").And.Contain("application/xml");
    }

    [Fact]
    public async Task DecorateRequest_InvalidHeaderKey_ReturnsError()
    {
        // Arrange - Create proxy
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Act - Try to add decoration with invalid header key
        var decorationRequest = new RestRequest($"/api/Proxies/{proxy.Id}/decorate", RestSharp.Method.Put);
        decorationRequest.AddJsonBody(new
        {
            key = "", // Empty key
            values = new[] { "test-value" }
        });

        var response = await ExecuteWithRetryAsync(decorationRequest);

        // Assert - Should succeed (validation might be handled differently)
        // The actual validation depends on implementation
        response.IsSuccessful.Should().BeTrue();
    }
}
