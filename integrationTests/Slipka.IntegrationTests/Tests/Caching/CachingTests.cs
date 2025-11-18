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

namespace Slipka.IntegrationTests.Tests.Caching;

public class CachingTests : IntegrationTestBase
{
    public CachingTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CacheResponse_Attribute_AddsCacheHeaders()
    {
        // Arrange - Create proxy that targets the test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to an endpoint that uses cache response attribute
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=test");

        // Assert - Response should include cache headers
        AssertSuccessStatusCode(response);

        // Check for cache control headers (these would be added by the CacheResponse attribute)
        var cacheControl = response.Headers.GetValues("Cache-Control").FirstOrDefault();
        cacheControl.Should().NotBeNull("Cache-Control header should be present");

        // Verify the response content
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("cached");
    }

    [Fact]
    public async Task CacheResponse_Attribute_VariesByQueryParameters()
    {
        // Arrange - Create proxy that targets the test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make requests with different query parameters
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=value1");
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=value2");

        // Assert - Both responses should be successful but potentially different
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        // Content should indicate different cache keys based on query params
        content1.Should().Contain("value1");
        content2.Should().Contain("value2");
    }

    [Fact]
    public async Task CacheResponse_Attribute_VariesByHeaders()
    {
        // Arrange - Create proxy that targets the test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make requests with different custom headers
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached",
            headers: new Dictionary<string, string> { ["X-Custom"] = "header1" });
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached",
            headers: new Dictionary<string, string> { ["X-Custom"] = "header2" });

        // Assert - Both responses should be successful
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        // Content should indicate different cache keys based on headers
        content1.Should().Contain("header1");
        content2.Should().Contain("header2");
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidatesSessionCache()
    {
        // Arrange - Create proxy and make some requests to populate cache
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Make some requests to populate cache
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=test1");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=test2");

        // Act - Call cache invalidation for the session
        var invalidationRequest = new RestRequest($"/api/Cache/session/{proxy.Id}", RestSharp.Method.Delete);
        var response = await ExecuteWithRetryAsync(invalidationRequest);

        // Assert - Invalidation should succeed
        response.IsSuccessful.Should().BeTrue();

        // Verify cache was invalidated by checking that subsequent requests work
        var verifyResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?param=test3");
        AssertSuccessStatusCode(verifyResponse);
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidatesProxyCache()
    {
        // Arrange - Create proxy and make some requests
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Make requests to populate proxy-related cache
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached");

        // Act - Call cache invalidation for the proxy
        var invalidationRequest = new RestRequest($"/api/Cache/proxy/{proxy.Id}", RestSharp.Method.Delete);
        var response = await ExecuteWithRetryAsync(invalidationRequest);

        // Assert - Invalidation should succeed
        response.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidatesAllSessionCache()
    {
        // Arrange - Create multiple proxies and populate cache
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy1.ProxyPort);
        await WaitForProxyReadyAsync(proxy2.ProxyPort);

        // Make requests for both proxies
        await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/cached?param=test1");
        await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/cached?param=test2");

        // Act - Call global session cache invalidation
        var invalidationRequest = new RestRequest("/api/Cache/sessions", RestSharp.Method.Delete);
        var response = await ExecuteWithRetryAsync(invalidationRequest);

        // Assert - Invalidation should succeed
        response.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task CacheInvalidationService_InvalidatesAllProxyCache()
    {
        // Arrange - Create multiple proxies
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy1.ProxyPort);
        await WaitForProxyReadyAsync(proxy2.ProxyPort);

        // Make requests to populate cache
        await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/cached");
        await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/cached");

        // Act - Call global proxy cache invalidation
        var invalidationRequest = new RestRequest("/api/Cache/proxies", RestSharp.Method.Delete);
        var response = await ExecuteWithRetryAsync(invalidationRequest);

        // Assert - Invalidation should succeed
        response.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task CacheResponse_Attribute_CustomDuration()
    {
        // Arrange - Create proxy for test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to endpoint with custom cache duration
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached-long");

        // Assert - Response should include appropriate cache headers
        AssertSuccessStatusCode(response);

        // Check for cache control header with custom duration
        var cacheControl = response.Headers.GetValues("Cache-Control").FirstOrDefault();
        cacheControl.Should().NotBeNull();
        cacheControl.Should().Contain("max-age=300"); // 5 minutes
    }

    [Fact]
    public async Task CacheResponse_Attribute_NoCacheWhenDisabled()
    {
        // Arrange - Create proxy for test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to endpoint that should not be cached
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/no-cache");

        // Assert - Response should not include cache headers
        AssertSuccessStatusCode(response);

        // Should not have cache control header
        response.Headers.Contains("Cache-Control").Should().BeFalse();
    }

    [Fact]
    public async Task CacheKeyGeneration_IncludesPathAndQuery()
    {
        // Arrange - Create proxy for test API
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request with path and query parameters
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?a=1&b=2");
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/cached?a=1&b=2");

        // Assert - Both requests should succeed
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        // The cache key should be the same for identical requests
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        // Content should indicate same cache key was used
        content1.Should().Be(content2);
    }
}
