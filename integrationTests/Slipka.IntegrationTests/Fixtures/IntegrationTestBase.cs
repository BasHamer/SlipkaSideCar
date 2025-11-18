using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using System.Net.Http;
using RestSharp;
using System.Collections.Generic;
using Polly;
using Polly.Retry;
using System.Threading.Tasks;

namespace Slipka.IntegrationTests.Fixtures;

/// <summary>
/// Base class for all integration tests that provides common functionality
/// including Slipka service health checks, proxy lifecycle management, and test API server.
/// </summary>
public class IntegrationTestBase : IClassFixture<TestApiFixture>, IAsyncLifetime
{
    protected readonly TestConfiguration Config;
    protected readonly RestClient SlipkaClient;
    protected readonly HttpClient HttpClient;
    protected readonly TestApiFixture TestApiFixture;

    // Collection of created proxies for cleanup
    protected readonly List<string> CreatedSessionIds = new();

    public IntegrationTestBase(TestApiFixture testApiFixture)
    {
        Config = new TestConfiguration();
        TestApiFixture = testApiFixture;

        var options = new RestClientOptions(Config.SlipkaBaseUrl);
        SlipkaClient = new RestClient(options);

        HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Config.DefaultRequestTimeoutSeconds)
        };
    }

    public async Task InitializeAsync()
    {
        // Wait for Slipka service to be healthy
        // Temporarily disabled for debugging
        // await WaitForSlipkaHealthAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup any test data
        await CleanupTestDataAsync();

        SlipkaClient.Dispose();
        HttpClient.Dispose();
    }

    protected async Task WaitForSlipkaHealthAsync(int timeoutSeconds = 30)
    {
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var response = await SlipkaClient.ExecuteAsync(healthRequest);
                if (response.IsSuccessful)
                {
                    var healthData = System.Text.Json.JsonSerializer.Deserialize<HealthResponse>(response.Content!);
                    if (healthData?.Status == "Healthy")
                    {
                        return;
                    }
                }
            }
            catch
            {
                // Slipka not ready yet
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Slipka service did not become healthy within {timeoutSeconds} seconds");
    }

    protected virtual async Task CleanupTestDataAsync()
    {
        // Clean up any proxies created during tests
        foreach (var sessionId in CreatedSessionIds.ToList())
        {
            try
            {
                await DeleteProxyAsync(sessionId);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        CreatedSessionIds.Clear();

        // Clean up any sessions
        try
        {
            var sessionsRequest = new RestRequest("/api/SessionsApi", RestSharp.Method.Delete);
            await SlipkaClient.ExecuteAsync(sessionsRequest);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    protected async Task<ProxyResponse> CreateProxyAsync(
        string targetHost = "localhost",
        int targetPort = 5001,
        string openFor = "00:05:00",
        string retainedFor = "00:10:00",
        bool proxyPortHttps = false,
        bool targetPortHttps = false)
    {
        var request = new RestRequest("/api/Proxies", RestSharp.Method.Post);
        request.AddJsonBody(new
        {
            targetHost,
            targetPort,
            openFor,
            retainedFor,
            proxyPortHttps,
            targetPortHttps
        });

        var response = await ExecuteWithRetryAsync(request);
        var proxyResponse = System.Text.Json.JsonSerializer.Deserialize<ProxyResponse>(response.Content!);

        if (proxyResponse != null)
        {
            CreatedSessionIds.Add(proxyResponse.Id);
        }

        return proxyResponse!;
    }

    protected async Task DeleteProxyAsync(string sessionId)
    {
        var request = new RestRequest($"/api/Proxies/{sessionId}", RestSharp.Method.Delete);
        await ExecuteWithRetryAsync(request);
        CreatedSessionIds.Remove(sessionId);
    }

    protected async Task WaitForProxyReadyAsync(int proxyPort, int timeoutSeconds = 10)
    {
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                // Try to make a request through the proxy to our test API
                using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await testClient.GetAsync($"http://localhost:{proxyPort}/api/test/echo?message=readiness");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Proxy not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Proxy on port {proxyPort} did not become ready within {timeoutSeconds} seconds");
    }

    protected async Task<RestResponse> ExecuteWithRetryAsync(RestRequest request, int maxRetries = 3)
    {
        var policy = Polly.Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(maxRetries, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        return await policy.ExecuteAsync(async () =>
        {
            var response = await SlipkaClient.ExecuteAsync(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException($"Request failed: {response.StatusCode} - {response.Content}");
            }
            return response;
        });
    }

    protected async Task<HttpResponseMessage> ExecuteHttpWithRetryAsync(
        HttpRequestMessage request,
        int maxRetries = 3)
    {
        var policy = Polly.Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(maxRetries, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        return await policy.ExecuteAsync(async () =>
        {
            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response;
        });
    }

    protected async Task<HttpResponseMessage> MakeProxyRequestAsync(
        int proxyPort,
        string path,
        HttpMethod method = null!,
        object body = null,
        Dictionary<string, string> headers = null)
    {
        method ??= HttpMethod.Get;

        var request = new HttpRequestMessage(method, $"http://localhost:{proxyPort}{path}");

        if (body != null)
        {
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json");
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        return await ExecuteHttpWithRetryAsync(request);
    }

    protected async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(content)!;
    }

    protected void AssertSuccessStatusCode(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected success status code but got {response.StatusCode}. Response: {response.Content?.ReadAsStringAsync().Result}");
    }

    protected void AssertStatusCode(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
    {
        response.StatusCode.Should().Be(expectedStatusCode,
            $"Expected status code {expectedStatusCode} but got {response.StatusCode}. Response: {response.Content?.ReadAsStringAsync().Result}");
    }
}
