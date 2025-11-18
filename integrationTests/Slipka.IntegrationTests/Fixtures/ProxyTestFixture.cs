using System;
using System.Linq;
using System.Net;
using RestSharp;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Xunit;

namespace Slipka.IntegrationTests.Fixtures;

public class ProxyTestFixture : IntegrationTestFixture
{
    private readonly List<string> _createdSessionIds = new();

    public async Task<ProxyResponse> CreateProxyAsync(string targetHost = "httpbin.org", int targetPort = 80, string openFor = "00:05:00", string retainedFor = "00:10:00", bool proxyPortHttps = false, bool targetPortHttps = false)
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
        var proxyResponse = JsonSerializer.Deserialize<ProxyResponse>(response.Content);

        if (proxyResponse != null)
        {
            _createdSessionIds.Add(proxyResponse.Id);
        }

        return proxyResponse;
    }

    public async Task DeleteProxyAsync(string sessionId)
    {
        var request = new RestRequest($"/api/Proxies/{sessionId}", RestSharp.Method.Delete);
        await ExecuteWithRetryAsync(request);
        _createdSessionIds.Remove(sessionId);
    }

    protected override async Task CleanupTestDataAsync()
    {
        await base.CleanupTestDataAsync();

        // Clean up any proxies created during tests
        foreach (var sessionId in _createdSessionIds.ToList())
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

        _createdSessionIds.Clear();
    }

    public async Task WaitForProxyReadyAsync(int proxyPort, int timeoutSeconds = 10)
    {
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                var response = await testClient.GetAsync($"http://localhost:{proxyPort}/status/200");
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
}

public class ProxyResponse
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
