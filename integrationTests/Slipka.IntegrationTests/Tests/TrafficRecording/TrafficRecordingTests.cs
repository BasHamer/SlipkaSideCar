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

namespace Slipka.IntegrationTests.Tests.TrafficRecording;

public class TrafficRecordingTests : IntegrationTestBase
{
    public TrafficRecordingTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordRequests_AllRequestsAreCaptured()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording for all requests
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = ".*" // Record all GET requests
        });

        await ExecuteWithRetryAsync(recordRequest);

        // Act - Make several requests through the proxy
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test1");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/201");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/testuser");

        // Wait a bit for recording to complete
        await Task.Delay(1000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(3);

        var echoCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/echo"));
        var statusCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/status"));
        var userCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/user"));

        echoCall.Should().NotBeNull();
        statusCall.Should().NotBeNull();
        userCall.Should().NotBeNull();

        echoCall!.Method.Should().Be("GET");
        statusCall!.Method.Should().Be("GET");
        userCall!.Method.Should().Be("GET");
    }

    [Fact]
    public async Task RecordRequests_WithUriFilter_OnlyRecordsMatchingRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording only for user endpoints
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*" // Only record user requests
        });

        await ExecuteWithRetryAsync(recordRequest);

        // Act - Make requests to different endpoints
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/testuser1");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/testuser2");

        // Wait for recording
        await Task.Delay(1000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCount(2); // Only user requests should be recorded

        session.Calls.Should().AllSatisfy(call =>
        {
            call.Uri.Should().Contain("/api/test/user/");
            call.Method.Should().Be("GET");
        });
    }

    [Fact]
    public async Task RecordRequests_WithHeaderFilter_OnlyRecordsMatchingHeaders()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording only for requests with specific header
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            request = new
            {
                headers = new[]
                {
                    new { key = "X-Record-Me", values = new[] { "true" } }
                }
            }
        });

        await ExecuteWithRetryAsync(recordRequest);

        // Act - Make requests with and without the header
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test1");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test2",
            headers: new Dictionary<string, string> { ["X-Record-Me"] = "true" });
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test3");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test4",
            headers: new Dictionary<string, string> { ["X-Record-Me"] = "true" });

        // Wait for recording
        await Task.Delay(1000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCount(2); // Only requests with header should be recorded

        session.Calls.Should().AllSatisfy(call =>
        {
            call.Uri.Should().Contain("/api/test/echo");
            call.Method.Should().Be("GET");
        });
    }

    [Fact]
    public async Task RecordRequests_IncludesRequestAndResponseBodies()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording for POST requests to data endpoint
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "POST",
            uri = "/api/test/data"
        });

        await ExecuteWithRetryAsync(recordRequest);

        // Act - Make a POST request with body
        var requestBody = new
        {
            name = "Integration Test",
            value = 42,
            validate = true,
            tags = new[] { "test", "recording" }
        };

        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/data", HttpMethod.Post, requestBody);

        // Wait for recording
        await Task.Delay(1000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCount(1);

        var call = session.Calls[0];
        call.Method.Should().Be("POST");
        call.Uri.Should().Contain("/api/test/data");
        call.Duration.Should().BeGreaterThan(0);

        // Verify we can retrieve the request and response bodies
        var requestBodyRequest = new RestRequest($"/api/Sessions/{proxy.Id}/request/1", RestSharp.Method.Get);
        var responseBodyRequest = new RestRequest($"/api/Sessions/{proxy.Id}/response/1", RestSharp.Method.Get);

        var requestBodyResponse = await ExecuteWithRetryAsync(requestBodyRequest);
        var responseBodyResponse = await ExecuteWithRetryAsync(responseBodyRequest);

        requestBodyResponse.Content.Should().NotBeNullOrEmpty();
        responseBodyResponse.Content.Should().NotBeNullOrEmpty();

        // Parse request body
        var recordedRequest = System.Text.Json.JsonSerializer.Deserialize<TestDataRequest>(requestBodyResponse.Content!);
        recordedRequest.Should().NotBeNull();
        recordedRequest!.Name.Should().Be("Integration Test");
        recordedRequest.Value.Should().Be(42);
        recordedRequest.Tags.Should().Contain("test");
        recordedRequest.Tags.Should().Contain("recording");
    }

    [Fact]
    public async Task RecordRequests_WithDurationThreshold_OnlyRecordsSlowRequests()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording for requests taking longer than 500ms
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/slow/.*",
            duration = 500 // Only record if duration >= 500ms
        });

        await ExecuteWithRetryAsync(recordRequest);

        // Act - Make requests with different durations
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/200"); // Fast request
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/1000"); // Slow request
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/100"); // Fast request
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/800"); // Slow request

        // Wait for recording
        await Task.Delay(2000); // Wait for all requests to complete

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(2); // At least the slow requests

        // Verify recorded calls have appropriate durations
        foreach (var call in session.Calls)
        {
            call.Duration.Should().BeGreaterOrEqualTo(500);
            call.Uri.Should().MatchRegex("/api/test/slow/[0-9]+");
        }
    }

    [Fact]
    public async Task RecordRequests_MultipleRecordingRules_AllMatchingRequestsRecorded()
    {
        // Arrange
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Add multiple recording rules
        var recordRequest1 = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest1.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo"
        });
        await ExecuteWithRetryAsync(recordRequest1);

        var recordRequest2 = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest2.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/status/.*"
        });
        await ExecuteWithRetryAsync(recordRequest2);

        // Act - Make requests matching different rules
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/404");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/test"); // Should not be recorded

        // Wait for recording
        await Task.Delay(1000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCount(3); // Echo + 2 status requests

        var uris = session.Calls.Select(c => c.Uri).ToList();
        uris.Should().Contain(uri => uri.Contains("/api/test/echo"));
        uris.Should().Contain(uri => uri.Contains("/api/test/status/200"));
        uris.Should().Contain(uri => uri.Contains("/api/test/status/404"));
        uris.Should().NotContain(uri => uri.Contains("/api/test/user"));
    }
}

public class SessionData
{
    public string Id { get; set; } = string.Empty;
    public List<CallData> Calls { get; set; } = new();
}

public class CallData
{
    public int CallNumber { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long Duration { get; set; }
    public DateTime StartedAt { get; set; }
}

public class TestDataRequest
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool Validate { get; set; }
    public List<string> Tags { get; set; } = new();
}
