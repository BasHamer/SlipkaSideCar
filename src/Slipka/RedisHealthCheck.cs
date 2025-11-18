using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Slipka.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    /// <summary>
    /// Health check for Redis cache connectivity
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IDistributedCache _cache;
        private readonly RedisSettings _redisSettings;

        public RedisHealthCheck(IDistributedCache cache, RedisSettings redisSettings)
        {
            _cache = cache;
            _redisSettings = redisSettings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!_redisSettings.Enabled)
            {
                return HealthCheckResult.Healthy("Redis caching is disabled");
            }

            try
            {
                // Try to set and get a test value from Redis
                var testKey = $"{_redisSettings.InstanceName}:health-check:{Guid.NewGuid()}";
                var testValue = $"test-{DateTime.UtcNow.Ticks}";

                await _cache.SetStringAsync(testKey, testValue, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                }, cancellationToken);

                var retrievedValue = await _cache.GetStringAsync(testKey, cancellationToken);
                await _cache.RemoveAsync(testKey, cancellationToken);

                if (retrievedValue == testValue)
                {
                    return HealthCheckResult.Healthy("Redis cache is responding correctly");
                }
                else
                {
                    return HealthCheckResult.Unhealthy("Redis cache set/get test failed - values don't match");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Redis cache health check failed: {ex.Message}", ex);
            }
        }
    }
}
