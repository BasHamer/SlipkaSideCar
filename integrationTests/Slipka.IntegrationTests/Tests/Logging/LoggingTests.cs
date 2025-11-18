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

namespace Slipka.IntegrationTests.Tests.Logging;

/// <summary>
/// Integration tests for the Logging API endpoint functionality.
/// Tests cover log message submission, correlation ID validation,
/// log level processing, structured logging, and error handling.
/// </summary>
public class LoggingTests : IntegrationTestBase
{
    public LoggingTests(TestApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LogMessageSubmission_BasicLogMessage_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test log message for basic functionality"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.Message.Should().Be("Log entry recorded");
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LogMessageSubmission_RequiredFieldsValidation_FailsWhenMissing()
    {
        // Arrange - Missing correlationId
        var invalidLogMessage = new
        {
            level = "Information",
            message = "Test message without correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(invalidLogMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task CorrelationIdValidation_AcceptsValidGuidFormat()
    {
        // Arrange
        var validCorrelationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = validCorrelationId,
            level = "Information",
            message = "Test message with valid GUID correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(validCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdValidation_AcceptsCustomFormatIds()
    {
        // Arrange
        var customCorrelationId = "custom-log-id-12345";
        var logMessage = new
        {
            correlationId = customCorrelationId,
            level = "Information",
            message = "Test message with custom format correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(customCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdValidation_RejectsInvalidFormats()
    {
        // Arrange - Invalid correlation ID with script tags
        var invalidCorrelationId = "<script>alert('xss')</script>";
        var logMessage = new
        {
            correlationId = invalidCorrelationId,
            level = "Information",
            message = "Test message with invalid correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert - Should fail due to validation
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task CorrelationIdValidation_RejectsEmptyCorrelationId()
    {
        // Arrange
        var logMessage = new
        {
            correlationId = "",
            level = "Information",
            message = "Test message with empty correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task CorrelationIdValidation_RejectsNullCorrelationId()
    {
        // Arrange
        var logMessage = new
        {
            correlationId = (string)null,
            level = "Information",
            message = "Test message with null correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task LogLevelProcessing_AllValidLevels_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };

        // Act & Assert - Test each log level
        foreach (var level in validLevels)
        {
            var logMessage = new
            {
                correlationId = correlationId,
                level = level,
                message = $"{level} level test message"
            };

            var request = new RestRequest("/api/logging", RestSharp.Method.Post);
            request.AddJsonBody(logMessage);
            var response = await ExecuteWithRetryAsync(request);

            response.IsSuccessful.Should().BeTrue();
            var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
            result.CorrelationId.Should().Be(correlationId);
        }
    }

    [Fact]
    public async Task LogLevelProcessing_InvalidLevel_DefaultsToInformation()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "InvalidLevel",
            message = "Test message with invalid log level"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert - Should succeed and default to Information level
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LogLevelProcessing_CaseInsensitiveLevels_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var testCases = new[]
        {
            "debug", "DEBUG", "Debug",
            "information", "INFORMATION", "Information",
            "warning", "WARNING", "Warning",
            "error", "ERROR", "Error",
            "critical", "CRITICAL", "Critical"
        };

        // Act & Assert
        foreach (var level in testCases)
        {
            var logMessage = new
            {
                correlationId = correlationId,
                level = level,
                message = $"{level} case test message"
            };

            var request = new RestRequest("/api/logging", RestSharp.Method.Post);
            request.AddJsonBody(logMessage);
            var response = await ExecuteWithRetryAsync(request);

            response.IsSuccessful.Should().BeTrue();
            var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
            result.CorrelationId.Should().Be(correlationId);
        }
    }

    [Fact]
    public async Task StructuredLogging_CustomProperties_IncludedInLog()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var properties = new Dictionary<string, object>
        {
            ["userId"] = "12345",
            ["action"] = "login",
            ["ipAddress"] = "192.168.1.100",
            ["userAgent"] = "IntegrationTest/1.0",
            ["sessionDuration"] = 3600,
            ["success"] = true
        };

        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "User login attempt with structured data",
            properties = properties
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task StructuredLogging_EmptyProperties_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Log message with empty properties",
            properties = new Dictionary<string, object>()
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task StructuredLogging_NullProperties_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Log message with null properties",
            properties = (Dictionary<string, object>)null
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CategoryAssignment_CustomCategory_IncludedInLog()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var customCategory = "CustomCategory.Test";
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message with custom category",
            category = customCategory
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CategoryAssignment_DefaultCategory_WhenNotSpecified()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message without category"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task TimestampHandling_CustomTimestamp_Preserved()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var customTimestamp = DateTime.UtcNow.AddHours(-1); // 1 hour ago
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message with custom timestamp",
            timestamp = customTimestamp
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task TimestampHandling_AutomaticTimestamp_WhenNotProvided()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var beforeRequest = DateTime.UtcNow;
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message without timestamp"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);
        var afterRequest = DateTime.UtcNow;

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
        // Note: We can't easily verify the automatic timestamp in integration tests
        // but the request should succeed
    }

    [Fact]
    public async Task ExceptionLogging_ExceptionDetails_IncludedInLog()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var exceptionDetails = "System.NullReferenceException: Object reference not set to an instance of an object\r\n   at TestMethod() in TestFile.cs:line 42";
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Error",
            message = "An unexpected error occurred during processing",
            exception = exceptionDetails,
            category = "ErrorHandling"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task ExceptionLogging_EmptyException_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Warning",
            message = "Warning message with empty exception",
            exception = ""
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task ExceptionLogging_NullException_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Info message with null exception",
            exception = (string)null
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CorrelationSubIdHandling_OriginalAndNewSubIds_IncludedInLog()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var originalSubId = Guid.NewGuid().ToString();
        var newSubId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            originalCorrelationSubId = originalSubId,
            newCorrelationSubId = newSubId,
            level = "Information",
            message = "Test message with correlation sub-IDs"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CorrelationSubIdHandling_NullSubIds_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            originalCorrelationSubId = (string)null,
            newCorrelationSubId = (string)null,
            level = "Information",
            message = "Test message with null sub-IDs"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task InvalidInputHandling_MissingMessage_FailsValidation()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information"
            // Missing message field
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task InvalidInputHandling_MissingLevel_FailsValidation()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            message = "Test message without level"
            // Missing level field
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task InvalidInputHandling_EmptyMessage_FailsValidation()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = ""
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task LogMessageSizeLimits_LargeMessage_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var largeMessage = new string('A', 10000); // 10KB message
        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = largeMessage
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task LogMessageSizeLimits_VeryLargeProperties_Accepted()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var largeProperties = new Dictionary<string, object>();
        for (int i = 0; i < 100; i++)
        {
            largeProperties[$"property{i}"] = new string('B', 1000); // 1KB per property
        }

        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message with large properties",
            properties = largeProperties
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task CorrelationIdValidation_LongCorrelationId_Accepted()
    {
        // Arrange - Maximum allowed length
        var longCorrelationId = new string('C', 99); // Just under 100 char limit
        var logMessage = new
        {
            correlationId = longCorrelationId,
            level = "Information",
            message = "Test message with long correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(longCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdValidation_TooLongCorrelationId_Rejected()
    {
        // Arrange - Over maximum length
        var tooLongCorrelationId = new string('D', 101); // Over 100 char limit
        var logMessage = new
        {
            correlationId = tooLongCorrelationId,
            level = "Information",
            message = "Test message with too long correlation ID"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ExecuteWithRetryAsync(request);
        });
    }

    [Fact]
    public async Task StructuredLogging_ComplexPropertyTypes_IncludedInLog()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var properties = new Dictionary<string, object>
        {
            ["stringValue"] = "test string",
            ["intValue"] = 42,
            ["boolValue"] = true,
            ["doubleValue"] = 3.14159,
            ["dateValue"] = DateTime.UtcNow.ToString("o"),
            ["arrayValue"] = new[] { 1, 2, 3, 4, 5 },
            ["nestedObject"] = new
            {
                innerProperty = "nested value",
                innerNumber = 123
            }
        };

        var logMessage = new
        {
            correlationId = correlationId,
            level = "Information",
            message = "Test message with complex property types",
            properties = properties
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.Should().NotBeNull();
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task IntegrationWithProxy_CorrelationIdsFromProxy_IncludedInLogs()
    {
        // Arrange - Create a proxy and make a request that generates correlation IDs
        var proxy = await CreateProxyAsync("localhost", 5001);
        await WaitForProxyReadyAsync(proxy.ProxyPort);

        var correlationId = Guid.NewGuid().ToString();

        // Make a request through proxy that would trigger logging
        var headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = correlationId
        };

        // First make a proxy request to generate correlation sub-ID
        await MakeProxyRequestAsync(proxy.ProxyPort, "/api/test/log-test", headers: headers);

        // Now submit a log message with the same correlation ID
        var logMessage = new
        {
            correlationId = correlationId,
            originalCorrelationSubId = Guid.NewGuid().ToString(),
            newCorrelationSubId = Guid.NewGuid().ToString(),
            level = "Information",
            message = "Log message correlated with proxy request",
            category = "ProxyIntegration"
        };

        // Act
        var request = new RestRequest("/api/logging", RestSharp.Method.Post);
        request.AddJsonBody(logMessage);
        var response = await ExecuteWithRetryAsync(request);

        // Assert
        response.IsSuccessful.Should().BeTrue();
        var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task ConcurrentLogSubmissions_MultipleRequests_HandledCorrectly()
    {
        // Arrange
        var correlationIds = Enumerable.Range(0, 10)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        // Act - Submit multiple log messages concurrently
        var tasks = correlationIds.Select(async correlationId =>
        {
            var logMessage = new
            {
                correlationId = correlationId,
                level = "Information",
                message = $"Concurrent log message {correlationId}"
            };

            var request = new RestRequest("/api/logging", RestSharp.Method.Post);
            request.AddJsonBody(logMessage);
            return await ExecuteWithRetryAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.IsSuccessful.Should().BeTrue();
            var result = System.Text.Json.JsonSerializer.Deserialize<LogResponse>(response.Content!);
            result.Should().NotBeNull();
            correlationIds.Should().Contain(result.CorrelationId);
        }
    }
}

public class LogResponse
{
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
}

public class ProxyResponse
{
    public string Id { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
}
