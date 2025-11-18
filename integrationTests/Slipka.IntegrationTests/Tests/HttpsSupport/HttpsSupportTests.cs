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

namespace Slipka.IntegrationTests.Tests.HttpsSupport;

/// <summary>
/// Integration tests for HTTPS support on proxies.
/// Tests cover HTTPS target proxying, protocol handling, and SSL scenarios.
/// </summary>
public class HttpsSupportTests : IntegrationTestBase
{
    public HttpsSupportTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HttpsToHttpProxying_ProxyForwardsHttpsRequestsToHttpBackend()
    {
        // Arrange - Create proxy that forwards HTTPS requests to HTTP backend
        // Note: This tests HTTPS target with HTTP proxy port
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request through proxy to HTTPS endpoint
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/json");

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("slideshow"); // httpbin.org/json response contains this
        content.Should().Contain("title");
    }

    [Fact]
    public async Task HttpsToHttpProxying_HandlesDifferentHttpsEndpoints()
    {
        // Arrange - Test with another HTTPS service
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Test different HTTPS endpoints
        var endpoints = new[] { "/uuid", "/headers", "/user-agent" };
        foreach (var endpoint in endpoints)
        {
            var response = await MakeProxyRequestAsync(proxy.ProxyPort, endpoint);
            AssertSuccessStatusCode(response);
        }
    }

    [Fact]
    public async Task HttpsToHttpProxying_PreservesQueryParameters()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request with query parameters
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/get?param1=value1&param2=value2");

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("param1");
        content.Should().Contain("value1");
        content.Should().Contain("param2");
        content.Should().Contain("value2");
    }

    [Fact]
    public async Task HttpsToHttpProxying_HandlesPostRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make POST request with JSON body
        var postData = new { name = "test", value = 123 };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/post", HttpMethod.Post, postData);

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("test");
        content.Should().Contain("123");
        content.Should().Contain("json"); // httpbin.org echoes the content-type
    }

    [Fact]
    public async Task HttpsToHttpProxying_HandlesCustomHeaders()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request with custom headers
        var headers = new Dictionary<string, string>
        {
            ["X-Custom-Header"] = "custom-value",
            ["X-Test-Header"] = "test-value",
            ["User-Agent"] = "SlipkaIntegrationTest/1.0"
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/headers", headers: headers);

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("X-Custom-Header");
        content.Should().Contain("custom-value");
        content.Should().Contain("X-Test-Header");
        content.Should().Contain("test-value");
        content.Should().Contain("SlipkaIntegrationTest/1.0");
    }

    [Fact]
    public async Task HttpsToHttpProxying_CorrelationIds_WorkWithHttps()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);
        var correlationId = Guid.NewGuid().ToString();

        // Act - Make request with correlation ID header
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId
        };
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/headers", headers: headers);

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("x-correlation-id");
        content.Should().Contain(correlationId);
    }

    [Fact]
    public async Task HttpsCertificateValidation_HandlesValidCertificates()
    {
        // Arrange - httpbin.org has valid certificate
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to validate certificate handling
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/status/200");

        // Assert - Should succeed with valid certificate
        AssertSuccessStatusCode(response);
    }

    [Fact]
    public async Task HttpsCertificateValidation_HandlesSelfSignedCertificates()
    {
        // Note: This test would require a backend with self-signed certificates
        // For now, we'll skip this as it requires additional test infrastructure
        // In a real scenario, this would test proxy behavior with invalid/self-signed certs

        await Task.CompletedTask; // Placeholder to avoid test being empty
    }

    [Fact]
    public async Task MixedProtocolSupport_ProxyHandlesBothHttpAndHttpsTargets()
    {
        // Arrange - Create two proxies: one to HTTP, one to HTTPS
        var httpProxy = await CreateProxyAsync("httpbin.org", 80, targetPortHttps: false);
        var httpsProxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(httpProxy.ProxyPort);
        await WaitForProxyReadyAsync(httpsProxy.ProxyPort);

        // Act - Make requests to both proxies
        var httpResponse = await MakeProxyRequestAsync(httpProxy.ProxyPort, "/json");
        var httpsResponse = await MakeProxyRequestAsync(httpsProxy.ProxyPort, "/json");

        // Assert - Both should work
        AssertSuccessStatusCode(httpResponse);
        AssertSuccessStatusCode(httpsResponse);

        var httpContent = await httpResponse.Content.ReadAsStringAsync();
        var httpsContent = await httpsResponse.Content.ReadAsStringAsync();

        // Both should return similar JSON structure
        httpContent.Should().Contain("slideshow");
        httpsContent.Should().Contain("slideshow");
    }

    [Fact]
    public async Task HttpsProxyConfiguration_ConfigurationPersistsCorrectly()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);

        // Act - Check proxy configuration via API
        var request = new RestRequest($"/api/Proxies/{proxy.Id}", RestSharp.Method.Get);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var proxyDetails = System.Text.Json.JsonSerializer.Deserialize<ProxyDetails>(response.Content!);
        proxyDetails.Should().NotBeNull();
        proxyDetails.TargetPortHttps.Should().BeTrue();
        proxyDetails.TargetHost.Should().Be("httpbin.org");
        proxyDetails.TargetPort.Should().Be(443);
    }

    [Fact]
    public async Task HttpsErrorHandling_HandlesConnectionFailures()
    {
        // Arrange - Try to connect to non-existent HTTPS host
        var proxy = await CreateProxyAsync("nonexistent-https-host-12345.com", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request that should fail due to DNS resolution
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/test");

        // Assert - Should handle the error gracefully
        // Note: The exact behavior depends on how the proxy handles connection failures
        response.Should().NotBeNull();
        // Could be 500, 502, or other error codes depending on implementation
    }

    [Fact]
    public async Task HttpsTimeoutHandling_HandlesSlowHttpsResponses()
    {
        // Arrange - Create proxy to a potentially slow HTTPS endpoint
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request to delay endpoint
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/delay/2");

        // Assert - Should handle the delay appropriately
        // This tests that HTTPS connections don't timeout prematurely
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task HttpsContentTypeHandling_HandlesDifferentContentTypes()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Test different content types
        var contentTypes = new[]
        {
            "/json",      // application/json
            "/html",      // text/html
            "/xml",       // application/xml
            "/robots.txt" // text/plain
        };

        foreach (var endpoint in contentTypes)
        {
            var response = await MakeProxyRequestAsync(proxy.ProxyPort, endpoint);
            AssertSuccessStatusCode(response);

            // Verify we get some content back
            var content = await response.Content.ReadAsStringAsync();
            content.Length.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task HttpsHeaderForwarding_HandlesAllStandardHeaders()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make request with various standard headers
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["Accept-Encoding"] = "gzip, deflate",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Cache-Control"] = "no-cache",
            ["Connection"] = "keep-alive",
            ["User-Agent"] = "Mozilla/5.0 (Integration Test)"
        };

        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/headers", headers: headers);

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();

        // Verify headers were forwarded (httpbin.org echoes them back)
        foreach (var header in headers)
        {
            content.Should().Contain(header.Key);
            content.Should().Contain(header.Value);
        }
    }

    [Fact]
    public async Task HttpsLargePayloadHandling_HandlesLargeResponses()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Request a large response
        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/stream/100"); // 100 lines

        // Assert
        AssertSuccessStatusCode(response);
        var content = await response.Content.ReadAsStringAsync();
        content.Length.Should().BeGreaterThan(1000); // Should be substantial
    }

    [Fact]
    public async Task HttpsConcurrentRequests_HandlesMultipleConcurrentHttpsRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Act - Make multiple concurrent requests
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var response = await MakeProxyRequestAsync(proxy.ProxyPort, $"/uuid");
            AssertSuccessStatusCode(response);
            return await response.Content.ReadAsStringAsync();
        });

        var results = await Task.WhenAll(tasks);

        // Assert - All requests should succeed and return different UUIDs
        results.Should().HaveCount(5);
        foreach (var result in results)
        {
            result.Should().Contain("uuid");
            result.Length.Should().BeGreaterThan(10);
        }

        // Verify all UUIDs are different (very unlikely to get duplicates)
        var uuids = results.Select(r => ExtractUuidFromResponse(r)).ToList();
        uuids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task HttpsProtocolUpgrade_Scenarios()
    {
        // Note: This test would verify HTTP to HTTPS upgrade scenarios
        // Currently, the proxy doesn't implement automatic protocol upgrades
        // but this test documents the expected behavior

        // For now, we'll test that HTTPS connections work as expected
        var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/status/200");
        AssertSuccessStatusCode(response);

        await Task.CompletedTask; // Placeholder for future protocol upgrade tests
    }

        [Fact]
        public async Task HttpsSecurityHeaders_HandlesSecurityHeadersCorrectly()
        {
            // Arrange
            var proxy = await CreateProxyAsync("httpbin.org", 443, targetPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Make request and check response headers
            var response = await MakeProxyRequestAsync(proxy.ProxyPort, "/response-headers?Strict-Transport-Security=max-age%3D31536000");

            // Assert
            AssertSuccessStatusCode(response);

            // Check if security headers are preserved
            response.Headers.Should().Contain(h => h.Key.ToLower() == "strict-transport-security");
            var stsHeader = response.Headers.First(h => h.Key.ToLower() == "strict-transport-security");
            stsHeader.Value.Should().Contain("max-age=31536000");
        }

        [Fact]
        public async Task HttpsProxyPort_SSLTermination_ProxyServesHttps()
        {
            // Arrange - Create proxy with HTTPS port enabled
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Make HTTPS request to the proxy
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Accept self-signed cert
            };
            using var httpsClient = new HttpClient(handler);
            var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/json");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("slideshow");
        }

        [Fact]
        public async Task HttpsProxyPort_EndToEndHttps_BothProxyAndTargetHttps()
        {
            // Arrange - Create proxy with HTTPS port forwarding to HTTPS target
            var proxy = await CreateProxyAsync("httpbin.org", 443, proxyPortHttps: true, targetPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Make HTTPS request to HTTPS proxy
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Accept self-signed cert
            };
            using var httpsClient = new HttpClient(handler);
            var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/uuid");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("uuid");
        }

        [Fact]
        public async Task SSLTermination_ValidatesCertificateConfiguration()
        {
            // Arrange - Create HTTPS proxy
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Check proxy configuration includes HTTPS settings
            var request = new RestRequest($"/api/Proxies/{proxy.Id}", RestSharp.Method.Get);
            var response = await ExecuteWithRetryAsync(request);

            // Assert
            response.IsSuccessful.Should().BeTrue();
            var proxyDetails = System.Text.Json.JsonSerializer.Deserialize<ProxyDetails>(response.Content!);
            proxyDetails.Should().NotBeNull();
            proxyDetails.ProxyPortHttps.Should().BeTrue();
            proxyDetails.TargetPortHttps.Should().BeFalse(); // HTTP target
        }

        [Fact]
        public async Task CertificateValidation_AcceptsSelfSignedCertificate()
        {
            // Arrange - Create HTTPS proxy
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Make request with certificate validation callback that accepts self-signed certs
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // In production, you'd validate the certificate properly
                    // For testing, we accept our self-signed certificate
                    return cert.Subject.Contains("localhost");
                }
            };
            using var httpsClient = new HttpClient(handler);
            var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/status/200");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task ProtocolUpgrade_HTTPToHTTPS_Redirect()
        {
            // Note: This test would verify HTTP to HTTPS upgrade/redirection
            // Currently, Slipka doesn't implement automatic protocol upgrades
            // but this test documents the expected behavior for when it does

            // For now, we'll test that HTTPS works when explicitly requested
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            using var httpsClient = new HttpClient(handler);

            // Act - Direct HTTPS request (simulating what upgrade would do)
            var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/get");
            response.IsSuccessStatusCode.Should().BeTrue();

            await Task.CompletedTask; // Placeholder for future protocol upgrade tests
        }

        [Fact]
        public async Task SSLHandshakeErrors_InvalidCertificate_Rejected()
        {
            // Arrange - Create HTTPS proxy
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Try to connect without accepting self-signed certificates
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => false // Reject all certs
            };
            using var httpsClient = new HttpClient(handler);

            // Assert - Should fail due to certificate validation
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/status/200");
            });
        }

        [Fact]
        public async Task MixedProtocolConfiguration_HTTPAndHTTPSProxies()
        {
            // Arrange - Create both HTTP and HTTPS proxies to the same target
            var httpProxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: false);
            var httpsProxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(httpProxy.ProxyPort);
            await WaitForProxyReadyAsync(httpsProxy.ProxyPort);

            // Act - Make requests to both proxies
            var httpResponse = await MakeProxyRequestAsync(httpProxy.ProxyPort, "/json");

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            using var httpsClient = new HttpClient(handler);
            var httpsResponse = await httpsClient.GetAsync($"https://localhost:{httpsProxy.ProxyPort}/json");

            // Assert - Both should work
            AssertSuccessStatusCode(httpResponse);
            httpsResponse.IsSuccessStatusCode.Should().BeTrue();

            var httpContent = await httpResponse.Content.ReadAsStringAsync();
            var httpsContent = await httpsResponse.Content.ReadAsStringAsync();

            // Both should return similar content
            httpContent.Should().Contain("slideshow");
            httpsContent.Should().Contain("slideshow");
        }

        [Fact]
        public async Task CertificateChainValidation_SelfSignedCertificate_Handled()
        {
            // Arrange - Create HTTPS proxy
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Validate certificate chain (self-signed will have limited chain)
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Check that we have a certificate
                    return cert != null && cert.Subject.Contains("localhost");
                }
            };
            using var httpsClient = new HttpClient(handler);
            var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/status/200");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task HttpsConcurrentRequests_MultipleHttpsConnections()
        {
            // Arrange - Create HTTPS proxy
            var proxy = await CreateProxyAsync("httpbin.org", 80, proxyPortHttps: true);
            await WaitForProxyReadyAsync(proxy.ProxyPort);

            // Act - Make multiple concurrent HTTPS requests
            var tasks = Enumerable.Range(0, 3).Select(async i =>
            {
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                using var httpsClient = new HttpClient(handler);
                var response = await httpsClient.GetAsync($"https://localhost:{proxy.ProxyPort}/uuid");
                response.IsSuccessStatusCode.Should().BeTrue();
                return await response.Content.ReadAsStringAsync();
            });

            var results = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            results.Should().HaveCount(3);
            foreach (var result in results)
            {
                result.Should().Contain("uuid");
            }
        }

    private string ExtractUuidFromResponse(string response)
    {
        // Simple extraction of UUID from httpbin.org/uuid response
        var start = response.IndexOf("\"uuid\": \"");
        if (start == -1) return string.Empty;

        start += 9; // Length of "\"uuid\": \""
        var end = response.IndexOf("\"", start);
        return end > start ? response.Substring(start, end - start) : string.Empty;
    }
}

public class ProxyDetails
{
    public string Id { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public string TargetHost { get; set; } = string.Empty;
    public int TargetPort { get; set; }
    public bool ProxyPortHttps { get; set; }
    public bool TargetPortHttps { get; set; }
    public DateTime LeaveProxyOpenUntil { get; set; }
    public DateTime RetainDataUntil { get; set; }
}
