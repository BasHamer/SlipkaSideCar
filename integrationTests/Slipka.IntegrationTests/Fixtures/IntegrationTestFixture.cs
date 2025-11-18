using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;
using RestSharp;
using Polly;
using Polly.Retry;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;

namespace Slipka.IntegrationTests.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    protected readonly TestConfiguration Config;
    protected readonly RestClient SlipkaClient;
    protected readonly HttpClient HttpClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    public IntegrationTestFixture()
    {
        Config = new TestConfiguration();

        var options = new RestClientOptions(Config.SlipkaBaseUrl);
        SlipkaClient = new RestClient(options);

        HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Config.DefaultRequestTimeoutSeconds)
        };

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task InitializeAsync()
    {
        // Wait for Slipka service to be healthy
        await WaitForSlipkaHealthAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup any test data
        await CleanupTestDataAsync();

        SlipkaClient.Dispose();
        HttpClient.Dispose();
    }

    protected async Task WaitForSlipkaHealthAsync()
    {
        var healthRequest = new RestRequest("/health", RestSharp.Method.Get);

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await SlipkaClient.ExecuteAsync(healthRequest);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException($"Slipka health check failed: {response.StatusCode}");
            }

            var healthData = JsonSerializer.Deserialize<HealthResponse>(response.Content);
            if (healthData?.Status != "Healthy")
            {
                throw new Exception($"Slipka is not healthy: {healthData?.Status}");
            }
        });
    }

    protected virtual async Task CleanupTestDataAsync()
    {
        // Default implementation - override in derived classes if needed
        try
        {
            // Clean up any sessions created during tests
            var sessionsRequest = new RestRequest("/api/SessionsApi", RestSharp.Method.Delete);
            await SlipkaClient.ExecuteAsync(sessionsRequest);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    protected async Task<RestResponse> ExecuteWithRetryAsync(RestRequest request)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await SlipkaClient.ExecuteAsync(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException($"Request failed: {response.StatusCode} - {response.Content}");
            }
            return response;
        });
    }

    protected async Task<HttpResponseMessage> ExecuteHttpWithRetryAsync(HttpRequestMessage request)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response;
        });
    }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public List<HealthCheck> Checks { get; set; } = new();
}

public class HealthCheck
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
