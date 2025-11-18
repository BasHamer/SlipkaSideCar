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

namespace Slipka.IntegrationTests.Tests.CallTagging;

public class CallTaggingTests : IntegrationTestBase
{
    public CallTaggingTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TagCall_WithUriPattern_TagsMatchingRequests()
    {
        // Arrange - Create proxy and add tagging rule
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        // Enable recording first so we can see the tags
        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = ".*" // Record all requests
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule for user endpoints
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*",
            tags = new[] { "user-endpoint", "api-call" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make requests to different endpoints
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/456");

        // Wait for recording and tagging to complete
        await Task.Delay(2000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(3);

        var userCalls = session.Calls.Where(c => c.Uri.Contains("/api/test/user/")).ToList();
        var echoCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/echo"));

        userCalls.Should().HaveCount(2);
        userCalls.Should().AllSatisfy(call =>
        {
            call.Tags.Should().Contain("user-endpoint");
            call.Tags.Should().Contain("api-call");
        });

        echoCall.Should().NotBeNull();
        echoCall!.Tags.Should().NotContain("user-endpoint");
        echoCall.Tags.Should().NotContain("api-call");
    }

    [Fact]
    public async Task TagCall_WithMethodFilter_OnlyTagsMatchingMethods()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = ".*", // Record all methods
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule for POST requests only
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "POST",
            uri = "/api/test/data",
            tags = new[] { "post-request", "data-submission" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make GET and POST requests to the same endpoint
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/data");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/data", HttpMethod.Post,
            new { test = "data" });

        await Task.Delay(2000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(2);

        var calls = session.Calls.Where(c => c.Uri.Contains("/api/test/data")).ToList();
        calls.Should().HaveCount(2);

        var getCall = calls.FirstOrDefault(c => c.Method == "GET");
        var postCall = calls.FirstOrDefault(c => c.Method == "POST");

        getCall.Should().NotBeNull();
        postCall.Should().NotBeNull();

        getCall!.Tags.Should().NotContain("post-request");
        getCall.Tags.Should().NotContain("data-submission");

        postCall!.Tags.Should().Contain("post-request");
        postCall.Tags.Should().Contain("data-submission");
    }

    [Fact]
    public async Task TagCall_WithDurationThreshold_TagsSlowRequests()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/slow/.*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule for slow requests (> 300ms)
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/slow/.*",
            duration = 300, // Tag requests taking longer than 300ms
            tags = new[] { "slow-request", "performance-issue" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make requests with different durations
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/100"); // Fast
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/500"); // Slow
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/200"); // Fast
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/slow/800"); // Slow

        await Task.Delay(3000); // Wait for all requests to complete

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(4);

        var slowCalls = session.Calls.Where(c =>
            c.Duration.HasValue && c.Duration.Value > 300).ToList();
        var fastCalls = session.Calls.Where(c =>
            c.Duration.HasValue && c.Duration.Value <= 300).ToList();

        slowCalls.Should().HaveCountGreaterOrEqualTo(2);
        slowCalls.Should().AllSatisfy(call =>
        {
            call.Tags.Should().Contain("slow-request");
            call.Tags.Should().Contain("performance-issue");
        });

        fastCalls.Should().AllSatisfy(call =>
        {
            call.Tags.Should().NotContain("slow-request");
            call.Tags.Should().NotContain("performance-issue");
        });
    }

    [Fact]
    public async Task TagCall_MultipleTaggingRules_AppliesAllMatchingTags()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = ".*",
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add multiple tagging rules
        var tagRequest1 = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest1.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/.*",
            tags = new[] { "api-request" }
        });
        await ExecuteWithRetryAsync(tagRequest1);

        var tagRequest2 = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest2.AddJsonBody(new
        {
            method = "GET",
            uri = ".*/echo.*",
            tags = new[] { "echo-endpoint" }
        });
        await ExecuteWithRetryAsync(tagRequest2);

        // Act - Make request that matches both rules
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");

        await Task.Delay(1500);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(1);

        var call = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/echo"));
        call.Should().NotBeNull();
        call!.Tags.Should().Contain("api-request");
        call.Tags.Should().Contain("echo-endpoint");
    }

    [Fact]
    public async Task TagCall_WithStatusCodeFilter_TagsBasedOnResponseStatus()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/status/.*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule for error responses
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/status/.*",
            statusCode = "4..", // Any 4xx status
            tags = new[] { "client-error", "needs-attention" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make requests with different status codes
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200"); // Success
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/404"); // Not found
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/500"); // Server error

        await Task.Delay(2000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(3);

        var errorCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/status/404"));
        var successCall = session.Calls.FirstOrDefault(c => c.Uri.Contains("/api/test/status/200"));

        errorCall.Should().NotBeNull();
        successCall.Should().NotBeNull();

        errorCall!.Tags.Should().Contain("client-error");
        errorCall.Tags.Should().Contain("needs-attention");

        successCall!.Tags.Should().NotContain("client-error");
        successCall.Tags.Should().NotContain("needs-attention");
    }

    [Fact]
    public async Task TagCall_CustomTags_AreAppliedCorrectly()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule with custom meaningful tags
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            tags = new[] { "health-check", "monitoring", "system-test", "integration-suite" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make request
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=health");

        await Task.Delay(1500);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCount(1);

        var call = session.Calls[0];
        call.Tags.Should().Contain("health-check");
        call.Tags.Should().Contain("monitoring");
        call.Tags.Should().Contain("system-test");
        call.Tags.Should().Contain("integration-suite");
    }

    [Fact]
    public async Task TagCall_SessionTags_AreAggregated()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = ".*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add multiple tagging rules with different tags
        var tagRequest1 = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest1.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/user/.*",
            tags = new[] { "user-management" }
        });
        await ExecuteWithRetryAsync(tagRequest1);

        var tagRequest2 = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest2.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/echo",
            tags = new[] { "debug-endpoint" }
        });
        await ExecuteWithRetryAsync(tagRequest2);

        // Act - Make requests to both endpoints
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");

        await Task.Delay(2000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert - Session should aggregate all unique tags
        session.Should().NotBeNull();
        session!.Tags.Should().Contain("user-management");
        session.Tags.Should().Contain("debug-endpoint");
    }

    [Fact]
    public async Task TagCall_RegexUriPattern_MatchesComplexPatterns()
    {
        // Arrange - Create proxy and enable recording
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var recordRequest = new RestRequest($"/api/Proxies/{proxy.Id}/record", RestSharp.Method.Put);
        recordRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/.*"
        });
        await ExecuteWithRetryAsync(recordRequest);

        // Add tagging rule with complex regex pattern
        var tagRequest = new RestRequest($"/api/Proxies/{proxy.Id}/tag", RestSharp.Method.Put);
        tagRequest.AddJsonBody(new
        {
            method = "GET",
            uri = "/api/test/(user|admin)/\\d+", // Matches /api/test/user/123 or /api/test/admin/456
            tags = new[] { "privileged-access", "id-based" }
        });
        await ExecuteWithRetryAsync(tagRequest);

        // Act - Make requests to matching and non-matching endpoints
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/user/123");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/admin/456");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/echo?message=test");
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/status/200");

        await Task.Delay(2000);

        // Retrieve session data
        var sessionRequest = new RestRequest($"/api/SessionsApi/{proxy.Id}", RestSharp.Method.Get);
        var sessionResponse = await ExecuteWithRetryAsync(sessionRequest);
        var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionResponse.Content!);

        // Assert
        session.Should().NotBeNull();
        session!.Calls.Should().HaveCountGreaterOrEqualTo(4);

        var privilegedCalls = session.Calls.Where(c =>
            c.Uri.Contains("/api/test/user/") || c.Uri.Contains("/api/test/admin/")).ToList();
        var regularCalls = session.Calls.Where(c =>
            c.Uri.Contains("/api/test/echo") || c.Uri.Contains("/api/test/status")).ToList();

        privilegedCalls.Should().HaveCount(2);
        privilegedCalls.Should().AllSatisfy(call =>
        {
            call.Tags.Should().Contain("privileged-access");
            call.Tags.Should().Contain("id-based");
        });

        regularCalls.Should().AllSatisfy(call =>
        {
            call.Tags.Should().NotContain("privileged-access");
            call.Tags.Should().NotContain("id-based");
        });
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
}
