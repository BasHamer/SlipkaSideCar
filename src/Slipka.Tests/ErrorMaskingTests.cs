using Slipka.Configuration;
using Slipka.Proxy;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;

namespace Slipka.Tests
{
    public class ErrorMaskingTests
    {
        [Fact]
        public void ErrorMaskingSettings_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var settings = new ErrorMaskingSettings();

            // Assert
            Assert.False(settings.Enabled);
            Assert.Null(settings.MaxErrorResponseLength);
            Assert.Null(settings.ErrorResponseRegex);
            Assert.Equal("An error occurred. Please check the logs for details using the correlation ID and correlation sub ID provided in the response headers.", settings.MaskedErrorMessage);
            Assert.Equal(new[] { 500, 502, 503, 504 }, settings.ErrorStatusCodes);
        }

        [Fact]
        public void ReverseProxyRoute_ErrorMasking_DefaultsToDisabled()
        {
            // Arrange & Act
            var route = new ReverseProxyRoute();

            // Assert
            Assert.NotNull(route.ErrorMasking);
            Assert.False(route.ErrorMasking.Enabled);
        }

        [Fact]
        public async Task ApplyErrorMaskingAsync_Disabled_DoesNotModifyResponse()
        {
            // Arrange
            var route = new ReverseProxyRoute
            {
                Id = "test-route",
                TargetHost = "localhost",
                TargetPort = 3000,
                ErrorMasking = new ErrorMaskingSettings { Enabled = false }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Original error content")
            };

            var loggerMock = new Mock<ILogger<ReverseProxyStartup>>();
            var httpContextMock = new Mock<HttpContext>();

            var startup = new ReverseProxyStartup(
                null, // configuration
                new ReverseProxySettings(),
                new AuthenticationSettings(),
                null, // fileRepository
                null, // messageRepository
                null, // sessionRepository
                null, // proxyStore
                null  // preprocessorFactory
            );

            // Use reflection to access the private method
            var method = typeof(ReverseProxyStartup).GetMethod("ApplyErrorMaskingAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var task = (Task)method.Invoke(startup, new object[] { httpContextMock.Object, responseMessage, route, "test-correlation-id", "test-correlation-sub-id", loggerMock.Object });
            await task;

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
            var content = await responseMessage.Content.ReadAsStringAsync();
            Assert.Equal("Original error content", content);
        }

        [Fact]
        public async Task ApplyErrorMaskingAsync_Enabled_MaxLengthExceeded_MasksResponse()
        {
            // Arrange
            var route = new ReverseProxyRoute
            {
                Id = "test-route",
                TargetHost = "localhost",
                TargetPort = 3000,
                ErrorMasking = new ErrorMaskingSettings
                {
                    Enabled = true,
                    MaxErrorResponseLength = 10,
                    MaskedErrorMessage = "Masked error message"
                }
            };

            var longErrorContent = "This is a very long error message that exceeds the maximum length";
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(longErrorContent)
            };

            var loggerMock = new Mock<ILogger<ReverseProxyStartup>>();
            var httpContextMock = new Mock<HttpContext>();

            var startup = new ReverseProxyStartup(
                null, // configuration
                new ReverseProxySettings(),
                new AuthenticationSettings(),
                null, // fileRepository
                null, // messageRepository
                null, // sessionRepository
                null, // proxyStore
                null  // preprocessorFactory
            );

            // Use reflection to access the private method
            var method = typeof(ReverseProxyStartup).GetMethod("ApplyErrorMaskingAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var task = (Task)method.Invoke(startup, new object[] { httpContextMock.Object, responseMessage, route, "test-correlation-id", "test-correlation-sub-id", loggerMock.Object });
            await task;

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
            var content = await responseMessage.Content.ReadAsStringAsync();
            Assert.Contains("Masked error message", content);
            Assert.Contains("test-correlation-id", content);
            Assert.Contains("test-correlation-sub-id", content);
            Assert.Contains("Correlation ID:", content);
            Assert.Contains("Correlation Sub ID:", content);

            // Verify correlation headers are added
            Assert.True(responseMessage.Headers.Contains("x-correlation-id"));
            Assert.True(responseMessage.Headers.Contains("x-correlation-sub-id"));
        }

        [Fact]
        public async Task ApplyErrorMaskingAsync_Enabled_RegexMatch_MasksResponse()
        {
            // Arrange
            var route = new ReverseProxyRoute
            {
                Id = "test-route",
                TargetHost = "localhost",
                TargetPort = 3000,
                ErrorMasking = new ErrorMaskingSettings
                {
                    Enabled = true,
                    ErrorResponseRegex = "(?i)stack\\s*trace",
                    MaskedErrorMessage = "Regex masked error"
                }
            };

            var errorContent = "An error occurred with stack trace details";
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(errorContent)
            };

            var loggerMock = new Mock<ILogger<ReverseProxyStartup>>();
            var httpContextMock = new Mock<HttpContext>();

            var startup = new ReverseProxyStartup(
                null, // configuration
                new ReverseProxySettings(),
                new AuthenticationSettings(),
                null, // fileRepository
                null, // messageRepository
                null, // sessionRepository
                null, // proxyStore
                null  // preprocessorFactory
            );

            // Use reflection to access the private method
            var method = typeof(ReverseProxyStartup).GetMethod("ApplyErrorMaskingAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var task = (Task)method.Invoke(startup, new object[] { httpContextMock.Object, responseMessage, route, "test-correlation-id", "test-correlation-sub-id", loggerMock.Object });
            await task;

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
            var content = await responseMessage.Content.ReadAsStringAsync();
            Assert.Contains("Regex masked error", content);
            Assert.Contains("test-correlation-id", content);
            Assert.Contains("test-correlation-sub-id", content);
        }

        [Fact]
        public async Task ApplyErrorMaskingAsync_Enabled_WrongStatusCode_DoesNotMask()
        {
            // Arrange
            var route = new ReverseProxyRoute
            {
                Id = "test-route",
                TargetHost = "localhost",
                TargetPort = 3000,
                ErrorMasking = new ErrorMaskingSettings
                {
                    Enabled = true,
                    MaxErrorResponseLength = 10,
                    MaskedErrorMessage = "Should not be masked"
                }
            };

            var longErrorContent = "This is a very long error message that exceeds the maximum length";
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK) // Not an error status code
            {
                Content = new StringContent(longErrorContent)
            };

            var loggerMock = new Mock<ILogger<ReverseProxyStartup>>();
            var httpContextMock = new Mock<HttpContext>();

            var startup = new ReverseProxyStartup(
                null, // configuration
                new ReverseProxySettings(),
                new AuthenticationSettings(),
                null, // fileRepository
                null, // messageRepository
                null, // sessionRepository
                null, // proxyStore
                null  // preprocessorFactory
            );

            // Use reflection to access the private method
            var method = typeof(ReverseProxyStartup).GetMethod("ApplyErrorMaskingAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var task = (Task)method.Invoke(startup, new object[] { httpContextMock.Object, responseMessage, route, "test-correlation-id", "test-correlation-sub-id", loggerMock.Object });
            await task;

            // Assert
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            var content = await responseMessage.Content.ReadAsStringAsync();
            Assert.Equal(longErrorContent, content); // Content should remain unchanged
        }
    }
}
