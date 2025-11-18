using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Slipka.Caching;
using Slipka.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Slipka.Tests
{
    public class CachingTests
    {
        [Fact]
        public void RedisSettings_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var settings = new RedisSettings();

            // Assert
            Assert.Equal("redis:6379", settings.ConnectionString);
            Assert.Equal("slipka", settings.InstanceName);
            Assert.Equal(0, settings.DefaultDatabase);
            Assert.True(settings.Enabled);
        }

        [Fact]
        public async Task CacheInvalidationService_InvalidateSessionCache_WhenRedisDisabled_DoesNothing()
        {
            // Arrange
            var mockCache = new Mock<IDistributedCache>();
            var mockLogger = new Mock<ILogger<CacheInvalidationService>>();
            var redisSettings = new RedisSettings { Enabled = false };
            var service = new CacheInvalidationService(mockCache.Object, redisSettings, mockLogger.Object);

            // Act
            await service.InvalidateSessionCacheAsync();

            // Assert
            mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), default), Times.Never);
            mockLogger.VerifyLog(LogLevel.Debug, "Redis caching is disabled, skipping cache invalidation");
        }

        [Fact]
        public async Task CacheInvalidationService_InvalidateSessionCache_WhenRedisEnabled_RemovesCacheEntries()
        {
            // Arrange
            var mockCache = new Mock<IDistributedCache>();
            var mockLogger = new Mock<ILogger<CacheInvalidationService>>();
            var redisSettings = new RedisSettings { Enabled = true, InstanceName = "test" };
            var service = new CacheInvalidationService(mockCache.Object, redisSettings, mockLogger.Object);

            // Act
            await service.InvalidateSessionCacheAsync("test-session");

            // Assert
            mockCache.Verify(c => c.RemoveAsync("test:sessions:test-session", default), Times.Once);
            mockCache.Verify(c => c.RemoveAsync("test:sessions:test-session:calls", default), Times.Once);
        }

        [Fact]
        public async Task CacheInvalidationService_InvalidateProxyCache_WhenRedisEnabled_RemovesCacheEntries()
        {
            // Arrange
            var mockCache = new Mock<IDistributedCache>();
            var mockLogger = new Mock<ILogger<CacheInvalidationService>>();
            var redisSettings = new RedisSettings { Enabled = true, InstanceName = "test" };
            var service = new CacheInvalidationService(mockCache.Object, redisSettings, mockLogger.Object);

            // Act
            await service.InvalidateProxyCacheAsync("test-proxy");

            // Assert
            mockCache.Verify(c => c.RemoveAsync("test:proxies:test-proxy", default), Times.Once);
        }

        [Fact]
        public async Task RedisHealthCheck_WhenRedisDisabled_ReturnsHealthy()
        {
            // Arrange
            var mockCache = new Mock<IDistributedCache>();
            var redisSettings = new RedisSettings { Enabled = false };
            var healthCheck = new RedisHealthCheck(mockCache.Object, redisSettings);

            // Act
            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("Redis caching is disabled", result.Description);
        }
    }

    // Extension method to make testing logging easier
    public static class MockLoggerExtensions
    {
        public static void VerifyLog<T>(this Mock<ILogger<T>> logger, LogLevel level, string message)
        {
            logger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
