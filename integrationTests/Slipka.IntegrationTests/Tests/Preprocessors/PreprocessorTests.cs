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

namespace Slipka.IntegrationTests.Tests.Preprocessors;

public class PreprocessorTests : IntegrationTestBase
{
    public PreprocessorTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HeaderAuthenticationPreprocessor_AddsStaticAuthHeader()
    {
        // Arrange - Create proxy with header authentication preprocessor
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add header authentication preprocessor
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "HeaderAuthentication",
            headerName = "X-API-Key",
            headerValue = "test-api-key-123",
            uriPatterns = new[] { "/api/test/auth.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make request to an endpoint that should include auth header
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/auth-required");

        // Assert - Request should succeed due to authentication header
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("authenticated").And.Contain("test-api-key-123");
    }

    [Fact]
    public async Task HeaderAuthenticationPreprocessor_OnlyAppliesToMatchingUris()
    {
        // Arrange - Create proxy with preprocessor that only applies to specific URIs
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add header authentication preprocessor for specific URI pattern
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "HeaderAuthentication",
            headerName = "Authorization",
            headerValue = "Bearer restricted-token",
            uriPatterns = new[] { "/api/test/restricted.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make requests to different endpoints
        var restrictedResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/restricted/data");
        var publicResponse = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/public/data");

        // Assert - Restricted endpoint should get auth header, public should not
        AssertSuccessStatusCode(restrictedResponse);
        AssertSuccessStatusCode(publicResponse);

        var restrictedContent = await restrictedResponse.Content.ReadAsStringAsync();
        var publicContent = await publicResponse.Content.ReadAsStringAsync();

        restrictedContent.Should().Contain("Bearer restricted-token");
        publicContent.Should().NotContain("Bearer restricted-token");
    }

    [Fact]
    public async Task HeaderAuthenticationPreprocessor_MultipleHeaders()
    {
        // Arrange - Create proxy with multiple header authentication preprocessors
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add first preprocessor
        var preprocessorRequest1 = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest1.AddJsonBody(new
        {
            type = "HeaderAuthentication",
            headerName = "X-API-Key",
            headerValue = "api-key-123"
        });
        await ExecuteWithRetryAsync(preprocessorRequest1);

        // Add second preprocessor
        var preprocessorRequest2 = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest2.AddJsonBody(new
        {
            type = "HeaderAuthentication",
            headerName = "X-User-Token",
            headerValue = "user-token-456"
        });
        await ExecuteWithRetryAsync(preprocessorRequest2);

        // Act - Make request that should include both headers
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/multi-auth");

        // Assert - Response should contain both authentication headers
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("api-key-123").And.Contain("user-token-456");
    }

    [Fact]
    public async Task SessionBasedPreprocessor_AuthenticatesAndMaintainsSession()
    {
        // Arrange - Create proxy with session-based preprocessor
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add session-based preprocessor that simulates login
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "SessionBased",
            loginUrl = $"{Config.TestApiBaseUrl}/api/test/login",
            loginMethod = "POST",
            loginBody = "{\"username\":\"testuser\",\"password\":\"testpass\"}",
            tokenExtractor = "json:token",
            headerTemplate = "Bearer {token}",
            sessionDuration = "00:30:00", // 30 minutes
            uriPatterns = new[] { "/api/test/protected.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make request to protected endpoint (should trigger authentication)
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/data");

        // Make another request (should reuse session)
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/info");

        // Assert - Both requests should succeed with valid authentication
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        content1.Should().Contain("authenticated").And.Contain("session");
        content2.Should().Contain("authenticated").And.Contain("session");
    }

    [Fact]
    public async Task SessionBasedPreprocessor_TokenRefreshAfterExpiry()
    {
        // Arrange - Create proxy with short-lived session
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add session-based preprocessor with very short session duration
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "SessionBased",
            loginUrl = $"{Config.TestApiBaseUrl}/api/test/login",
            loginMethod = "POST",
            loginBody = "{\"username\":\"testuser\",\"password\":\"testpass\"}",
            tokenExtractor = "json:token",
            headerTemplate = "Bearer {token}",
            sessionDuration = "00:00:05", // 5 seconds - very short
            uriPatterns = new[] { "/api/test/protected.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make first request (should authenticate)
        var response1 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/data");

        // Wait for token to expire
        await Task.Delay(6000); // Wait 6 seconds

        // Make second request (should re-authenticate)
        var response2 = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/data");

        // Assert - Both requests should succeed
        AssertSuccessStatusCode(response1);
        AssertSuccessStatusCode(response2);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        content1.Should().Contain("authenticated");
        content2.Should().Contain("authenticated");
    }

    [Fact]
    public async Task SessionBasedPreprocessor_HandlesLoginFailure()
    {
        // Arrange - Create proxy with invalid login credentials
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add session-based preprocessor with invalid credentials
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "SessionBased",
            loginUrl = $"{Config.TestApiBaseUrl}/api/test/login",
            loginMethod = "POST",
            loginBody = "{\"username\":\"invalid\",\"password\":\"wrong\"}",
            tokenExtractor = "json:token",
            headerTemplate = "Bearer {token}",
            sessionDuration = "00:30:00",
            uriPatterns = new[] { "/api/test/protected.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make request to protected endpoint (should fail authentication)
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/data");

        // Assert - Request should fail due to authentication failure
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("authentication").And.Contain("failed");
    }

    [Fact]
    public async Task SessionBasedPreprocessor_CustomHeaderTemplate()
    {
        // Arrange - Create proxy with custom header template
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add session-based preprocessor with custom header format
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "SessionBased",
            loginUrl = $"{Config.TestApiBaseUrl}/api/test/login",
            loginMethod = "POST",
            loginBody = "{\"username\":\"testuser\",\"password\":\"testpass\"}",
            tokenExtractor = "json:token",
            headerTemplate = "Token token=\"{token}\"",
            sessionDuration = "00:30:00",
            uriPatterns = new[] { "/api/test/protected.*" }
        });

        await ExecuteWithRetryAsync(preprocessorRequest);

        // Act - Make request to protected endpoint
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/protected/data");

        // Assert - Request should succeed with custom header format
        AssertSuccessStatusCode(response);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Token token=").And.Contain("authenticated");
    }

    [Fact]
    public async Task PreprocessorFactory_ValidatesPreprocessorTypes()
    {
        // Arrange - Create proxy
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Act - Try to add invalid preprocessor type
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "InvalidPreprocessorType",
            headerName = "X-Test",
            headerValue = "test"
        });

        var response = await ExecuteWithRetryAsync(preprocessorRequest);

        // Assert - Should return bad request for invalid type
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Should().Contain("InvalidPreprocessorType");
    }

    [Fact]
    public async Task Preprocessor_HandlesInvalidConfiguration()
    {
        // Arrange - Create proxy
        var proxy = await CreateProxyAsync("localhost", 5001);

        // Act - Try to add preprocessor with invalid configuration
        var preprocessorRequest = new RestRequest($"/api/Proxies/{proxy.Id}/preprocessor", RestSharp.Method.Put);
        preprocessorRequest.AddJsonBody(new
        {
            type = "HeaderAuthentication",
            // Missing required headerName and headerValue
            uriPatterns = new[] { "/api/test/.*" }
        });

        var response = await ExecuteWithRetryAsync(preprocessorRequest);

        // Assert - Should return bad request for invalid configuration
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Should().Contain("configuration").And.Contain("invalid");
    }
}
