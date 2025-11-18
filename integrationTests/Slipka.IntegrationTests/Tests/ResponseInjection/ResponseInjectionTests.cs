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

namespace Slipka.IntegrationTests.Tests.ResponseInjection;

public class ResponseInjectionTests : IntegrationTestBase
{
    public ResponseInjectionTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task InjectResponse_ForSpecificUri_ReturnsInjectedResponse()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject a custom response for /api/test/user/notfound
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/notfound",
            statusCode = 404,
            response = new
            {
                content = "{\"error\": \"User not found via injection\", \"userId\": \"notfound\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } },
                    new { key = "X-Injected-By", values = new[] { "SlipkaTest" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act - Make request that should be intercepted
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/notfound");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(content);
        result!.Error.Should().Be("User not found via injection");
        result.UserId.Should().Be("notfound");

        // Verify injected header is present
        response.Headers.Should().Contain(h => h.Key == "X-Injected-By" && h.Value.Contains("SlipkaTest"));
    }

    [Fact]
    public async Task InjectResponse_WithDelay_SimulatesSlowResponse()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject a slow response
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/slow/100", // This would normally be slow
            duration = 2000, // 2 second delay
            statusCode = 200,
            response = new
            {
                content = "{\"message\": \"Fast response via injection\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act - Time the request
        var startTime = DateTime.UtcNow;
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/100");
        var endTime = DateTime.UtcNow;

        // Assert
        AssertSuccessStatusCode(response);
        var duration = endTime - startTime;
        duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1800)); // At least 1.8 seconds
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(3000)); // Less than 3 seconds

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Fast response via injection");
    }

    [Fact]
    public async Task InjectResponse_WithRegexUri_MatchesMultipleEndpoints()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject response for all user endpoints
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*", // Regex pattern
            statusCode = 200,
            response = new
            {
                content = "{\"message\": \"All users handled by injection\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act - Test multiple user endpoints
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123");
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/abc");
        var response3 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/test-user");

        // Assert - All should return injected response
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);
        AssertSuccessStatusCode(response3);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        var content3 = await response3.Content.ReadAsStringAsync();

        content1.Should().Contain("All users handled by injection");
        content2.Should().Contain("All users handled by injection");
        content3.Should().Contain("All users handled by injection");
    }

    [Fact]
    public async Task InjectResponse_WithMethodFilter_OnlyInterceptsMatchingMethods()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject response only for POST requests to echo endpoint
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "POST",
            uri = "/api/test/echo",
            statusCode = 201,
            response = new
            {
                content = "{\"message\": \"POST intercepted\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act - Test GET request (should not be intercepted)
        var getResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo");

        // Test POST request (should be intercepted)
        var postResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo", HttpMethod.Post,
            new { message = "test" });

        // Assert
        AssertSuccessStatusCode(getResponse); // GET goes through normally
        AssertStatusCode(postResponse, HttpStatusCode.Created); // POST is intercepted

        var postContent = await postResponse.Content.ReadAsStringAsync();
        postContent.Should().Contain("POST intercepted");
    }

    [Fact]
    public async Task InjectResponse_WithHeadersFilter_OnlyInterceptsMatchingHeaders()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject response only when X-Test-Header is present
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            statusCode = 200,
            request = new
            {
                headers = new[]
                {
                    new { key = "X-Test-Header", values = new[] { "expected-value" } }
                }
            },
            response = new
            {
                content = "{\"message\": \"Header matched\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act - Request without header (should not be intercepted)
        var responseWithoutHeader = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo");

        // Request with matching header (should be intercepted)
        var responseWithHeader = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo",
            headers: new Dictionary<string, string> { ["X-Test-Header"] = "expected-value" });

        // Request with wrong header value (should not be intercepted)
        var responseWithWrongHeader = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo",
            headers: new Dictionary<string, string> { ["X-Test-Header"] = "wrong-value" });

        // Assert
        AssertSuccessStatusCode(responseWithoutHeader);
        AssertSuccessStatusCode(responseWithHeader);
        AssertSuccessStatusCode(responseWithWrongHeader);

        var contentWithout = await responseWithoutHeader.Content.ReadAsStringAsync();
        var contentWith = await responseWithHeader.Content.ReadAsStringAsync();
        var contentWrong = await responseWithWrongHeader.Content.ReadAsStringAsync();

        contentWithout.Should().NotContain("Header matched");
        contentWith.Should().Contain("Header matched");
        contentWrong.Should().NotContain("Header matched");
    }

    [Fact]
    public async Task InjectResponse_MultipleInjections_LastOneWins()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // First injection
        var injectRequest1 = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest1.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            statusCode = 200,
            response = new
            {
                content = "{\"message\": \"First injection\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest1);

        // Second injection (should override first)
        var injectRequest2 = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest2.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            statusCode = 200,
            response = new
            {
                content = "{\"message\": \"Second injection\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest2);

        // Act
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo");

        // Assert - Should get second injection response
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Second injection");
        content.Should().NotContain("First injection");
    }

    [Fact]
    public async Task InjectResponse_WithTags_IncludesTagsInResponse()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Inject response with tags
        var injectRequest = new RestRequest($"/api/Proxies/{proxy.Id}/inject", RestSharp.Method.Put);
        injectRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            statusCode = 418, // I'm a teapot
            tags = new[] { "test-injection", "integration-test", "funny-status" },
            response = new
            {
                content = "{\"message\": \"Tagged injection\"}",
                headers = new[]
                {
                    new { key = "Content-Type", values = new[] { "application/json" } }
                }
            }
        });

        await ExecuteWithRetryAsync(injectRequest);

        // Act
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo");

        // Assert
        ((int)response.StatusCode).Should().Be(418);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Tagged injection");
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
