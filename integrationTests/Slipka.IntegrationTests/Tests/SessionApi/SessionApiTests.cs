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

namespace Slipka.IntegrationTests.Tests.SessionApi;

public class SessionApiTests : IntegrationTestBase
{
    public SessionApiTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetSession_ReturnsSessionData()
    {
        // Arrange - Create a proxy and make some requests to generate session data
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording to capture session data
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Make some requests
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test1");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200");

        await Task.Delay(1500); // Wait for recording

        // Act - Retrieve session data
        var getSessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(getSessionRequest);

        // Assert
        sessionResponse.IsSuccessful.Should().BeTrue();
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        session.Should().NotBeNull();
        session!.Id.Should().Be(proxy.Id);
        session.Calls.Should().HaveCountGreaterOrEqualTo(2);

        var echoCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/echo"));
        var statusCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/status"));

        echoCall.Should().NotBeNull();
        statusCall.Should().NotBeNull();
        echoCall!.Method.Should().Be("GET");
        statusCall!.Method.Should().Be("GET");
    }

    [Fact]
    public async Task GetSession_WithInvalidId_ReturnsNotFound()
    {
        // Act - Try to get a non-existent session
        var getSessionRequest = new RestRequest("/api/SessionsApi/non-existent-session-id", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(getSessionRequest);

        // Assert
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllSessions_ReturnsMultipleSessions()
    {
        // Arrange - Create multiple proxies to generate multiple sessions
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);

        // Make requests for each proxy
        await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/echo?message=session1");
        await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/echo?message=session2");

        await Task.Delay(1500); // Wait for processing

        // Act - Get all sessions
        var getSessionsRequest = new RestRequest("/api/SessionsApi", RestSharp.Method.Get);
        var sessionsResponse = await ExecuteWithRetryAsync(getSessionsRequest);

        // Assert
        sessionsResponse.IsSuccessful.Should().BeTrue();
        var sessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionData>>(sessionsResponse.Content!);

        sessions.Should().NotBeNull();
        sessions!.Should().HaveCountGreaterOrEqualTo(2);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        sessionIds.Should().Contain(proxy1.Id.ToString());
        sessionIds.Should().Contain(proxy2.Id.ToString());
    }

    [Fact]
    public async Task GetAllSessions_FilterByTag_ReturnsFilteredSessions()
    {
        // Arrange - Create proxies with different tags
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);

        // Add different tags to sessions
        var tagRequest1 = new RestRequest($"/api/Proxies/{proxy1.Id}/tag", RestSharp.Method.Put);
        tagRequest1.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/.*",
            tags = new[] { "production", "api" }
        });
        await ExecuteWithRetryAsync(tagRequest1);

        var tagRequest2 = new RestRequest($"/api/Proxies/{proxy2.Id}/tag", RestSharp.Method.Put);
        tagRequest2.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/.*",
            tags = new[] { "testing", "api" }
        });
        await ExecuteWithRetryAsync(tagRequest2);

        // Make requests to apply tags
        await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/echo?message=prod");
        await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/echo?message=test");

        await Task.Delay(2000); // Wait for tagging

        // Act - Filter sessions by tag
        var getSessionsRequest = new RestRequest("/api/SessionsApi?tag=production", RestSharp.Method.Get);
        var sessionsResponse = await ExecuteWithRetryAsync(getSessionsRequest);

        // Assert
        sessionsResponse.IsSuccessful.Should().BeTrue();
        var sessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionData>>(sessionsResponse.Content!);

        sessions.Should().NotBeNull();
        sessions!.Should().HaveCountGreaterOrEqualTo(1);

        var productionSession = sessions.FirstOrDefault(s => s.Id == proxy1.Id);
        productionSession.Should().NotBeNull();
        productionSession!.Tags.Should().Contain("production");

        // Verify testing session is not included in production filter
        sessions.Should().NotContain(s => s.Id == proxy2.Id);
    }

    [Fact]
    public async Task GetSessionCalls_ReturnsFilteredCalls()
    {
        // Arrange - Create proxy and enable recording with different criteria
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = ".*",
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging for categorization
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*",
            tags = new[] { "user-request" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Make various requests
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123"); // Tagged
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=fast"); // Fast
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/1500"); // Slow
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/456"); // Tagged

        await Task.Delay(2000); // Wait for all requests to complete

        // Act - Get calls filtered by tag
        var getCallsRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}/calls?tag=user-request", RestSharp.Method.Get);
        var callsResponse = await ExecuteWithRetryAsync(getCallsRequest);

        // Assert
        callsResponse.IsSuccessful.Should().BeTrue();
        var calls = System.Text.Json.JsonSerializer.Deserialize<List<CallData>>(callsResponse.Content!);

        calls.Should().NotBeNull();
        calls!.Should().HaveCount(2); // Only tagged calls
        calls.Should().AllSatisfy(call => call.Tags.Should().Contain("user-request"));
    }

    [Fact]
    public async Task GetSessionCalls_FilterByRecordedStatus()
    {
        // Arrange - Create proxy with selective recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Record only user endpoints
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Make requests to different endpoints
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123"); // Recorded
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test"); // Not recorded

        await Task.Delay(1500);

        // Act - Get only recorded calls
        var getCallsRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}/calls?recorded=true", RestSharp.Method.Get);
        var callsResponse = await ExecuteWithRetryAsync(getCallsRequest);

        // Assert
        callsResponse.IsSuccessful.Should().BeTrue();
        var calls = System.Text.Json.JsonSerializer.Deserialize<List<CallData>>(callsResponse.Content!);

        calls.Should().NotBeNull();
        calls!.Should().HaveCount(1);
        calls[0].Uri.Should().Contain("/api/test/user/");
        calls[0].Recorded.Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionCalls_FilterByMinimumDuration()
    {
        // Arrange - Create proxy and record all requests
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/slow/.*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Make requests with different durations
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/200"); // Fast
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/1000"); // Medium
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/2000"); // Slow

        await Task.Delay(2500); // Wait for all requests to complete

        // Act - Get calls with minimum duration of 800ms
        var getCallsRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}/calls?minimumDuration=800", RestSharp.Method.Get);
        var callsResponse = await ExecuteWithRetryAsync(getCallsRequest);

        // Assert
        callsResponse.IsSuccessful.Should().BeTrue();
        var calls = System.Text.Json.JsonSerializer.Deserialize<List<CallData>>(callsResponse.Content!);

        calls.Should().NotBeNull();
        calls!.Should().HaveCountGreaterOrEqualTo(2); // Medium and slow requests
        calls.Should().AllSatisfy(call =>
        {
            call.Duration.Should().NotBeNull();
            call.Duration!.Value.Should().BeGreaterOrEqualTo(800);
        });
    }

    [Fact]
    public async Task GetSessionCalls_WithInvalidSessionId_ReturnsNotFound()
    {
        // Act - Try to get calls for non-existent session
        var getCallsRequest = new RestRequest("/api/SessionsApi/invalid-session-id/calls", RestSharp.Method.Get);
        var callsResponse = await ExecuteWithRetryAsync(getCallsRequest);

        // Assert
        callsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_RemovesSessionSuccessfully()
    {
        // Arrange - Create a proxy and generate session data
        var proxy = await CreateProxyAsync("localhost", 5001);
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");

        await Task.Delay(1000);

        // Verify session exists
        var getSessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var initialResponse = await ExecuteWithRetryAsync(getSessionRequest);
        initialResponse.IsSuccessful.Should().BeTrue();

        // Act - Delete the session
        var deleteRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Delete);
        var deleteResponse = await ExecuteWithRetryAsync(deleteRequest);

        // Assert
        deleteResponse.IsSuccessful.Should().BeTrue();

        // Verify session is gone
        var afterDeleteResponse = await ExecuteWithRetryAsync(getSessionRequest);
        afterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAllSessions_RemovesAllSessions()
    {
        // Arrange - Create multiple proxies
        var proxy1 = await CreateProxyAsync("localhost", 5001);
        var proxy2 = await CreateProxyAsync("localhost", 5001);

        // Make requests to ensure sessions exist
        await MakeProxyRequestAsync(proxy1.ProxyPort, "/api/test/echo?message=test1");
        await MakeProxyRequestAsync(proxy2.ProxyPort, "/api/test/echo?message=test2");

        await Task.Delay(1000);

        // Verify sessions exist
        var getAllRequest = new RestRequest("/api/SessionsApi", RestSharp.Method.Get);
        var initialResponse = await ExecuteWithRetryAsync(getAllRequest);
        var initialSessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionData>>(initialResponse.Content!);
        initialSessions.Should().NotBeNull();
        initialSessions!.Count.Should().BeGreaterOrEqualTo(2);

        // Act - Delete all sessions
        var deleteAllRequest = new RestRequest("/api/SessionsApi", RestSharp.Method.Delete);
        var deleteResponse = await ExecuteWithRetryAsync(deleteAllRequest);

        // Assert
        deleteResponse.IsSuccessful.Should().BeTrue();

        // Verify all sessions are gone
        var afterDeleteResponse = await ExecuteWithRetryAsync(getAllRequest);
        var finalSessions = System.Text.Json.JsonSerializer.Deserialize<List<SessionData>>(afterDeleteResponse.Content!);
        finalSessions.Should().NotBeNull();
        finalSessions!.Should().BeEmpty();
    }

    [Fact]
    public async Task SessionApi_ComplexFiltering_MultipleCriteria()
    {
        // Arrange - Create proxy with comprehensive setup
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording and tagging
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = ".*",
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*",
            tags = new[] { "user", "authenticated" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Make diverse requests
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123"); // Tagged, recorded
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=fast"); // Not tagged, recorded
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/1500"); // Not tagged, recorded, slow

        await Task.Delay(2000); // Wait for processing

        // Act - Apply multiple filters: tag + minimum duration
        var getCallsRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}/calls?tag=user&minimumDuration=100", RestSharp.Method.Get);
        var callsResponse = await ExecuteWithRetryAsync(getCallsRequest);

        // Assert - Should return calls that match ALL criteria
        callsResponse.IsSuccessful.Should().BeTrue();
        var calls = System.Text.Json.JsonSerializer.Deserialize<List<CallData>>(callsResponse.Content!);

        calls.Should().NotBeNull();
        calls!.Should().HaveCount(1);
        calls[0].Tags.Should().Contain("user");
        calls[0].Tags.Should().Contain("authenticated");
        calls[0].Uri.Should().Contain("/api/test/user/123");
    }
}

public class SessionData
{
    public string Id { get; set; } = string.Empty;
    public List<CallData> Calls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class CallData
{
    public int CallNumber { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public double? Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool Recorded { get; set; }
}
