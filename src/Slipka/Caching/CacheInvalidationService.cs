using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Slipka.Configuration;
using System;
using System.Threading.Tasks;

namespace Slipka.Caching
{
    /// <summary>
    /// Implementation of cache invalidation service using Redis
    /// </summary>
    public class CacheInvalidationService : ICacheInvalidationService
    {
        private readonly IDistributedCache _cache;
        private readonly RedisSettings _redisSettings;
        private readonly ILogger<CacheInvalidationService> _logger;

        public CacheInvalidationService(
            IDistributedCache cache,
            RedisSettings redisSettings,
            ILogger<CacheInvalidationService> logger)
        {
            _cache = cache;
            _redisSettings = redisSettings;
            _logger = logger;
        }

        public async Task InvalidateSessionCacheAsync()
        {
            if (!_redisSettings.Enabled)
            {
                _logger.LogDebug("Redis caching is disabled, skipping cache invalidation");
                return;
            }

            try
            {
                // For Redis, we can use key patterns to delete multiple keys
                // Since IDistributedCache doesn't support pattern deletion directly,
                // we'll use a pattern-based approach with StackExchange.Redis
                var redis = await GetRedisDatabaseAsync();
                if (redis != null)
                {
                    var endpoints = redis.Multiplexer.GetEndPoints();
                    foreach (var endpoint in endpoints)
                    {
                        var server = redis.Multiplexer.GetServer(endpoint);
                        var keys = server.Keys(pattern: $"{_redisSettings.InstanceName}:sessions:*");

                        foreach (var key in keys)
                        {
                            await _cache.RemoveAsync(key);
                        }
                    }

                    _logger.LogInformation("Invalidated all session cache entries");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate session cache");
            }
        }

        public async Task InvalidateSessionCacheAsync(string sessionId)
        {
            if (!_redisSettings.Enabled)
            {
                _logger.LogDebug("Redis caching is disabled, skipping cache invalidation");
                return;
            }

            try
            {
                var cacheKey = $"{_redisSettings.InstanceName}:sessions:{sessionId}";
                await _cache.RemoveAsync(cacheKey);
                await _cache.RemoveAsync($"{cacheKey}:calls");

                _logger.LogInformation("Invalidated cache for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache for session {SessionId}", sessionId);
            }
        }

        public async Task InvalidateProxyCacheAsync()
        {
            if (!_redisSettings.Enabled)
            {
                _logger.LogDebug("Redis caching is disabled, skipping cache invalidation");
                return;
            }

            try
            {
                var redis = await GetRedisDatabaseAsync();
                if (redis != null)
                {
                    var endpoints = redis.Multiplexer.GetEndPoints();
                    foreach (var endpoint in endpoints)
                    {
                        var server = redis.Multiplexer.GetServer(endpoint);
                        var keys = server.Keys(pattern: $"{_redisSettings.InstanceName}:proxies:*");

                        foreach (var key in keys)
                        {
                            await _cache.RemoveAsync(key);
                        }
                    }

                    _logger.LogInformation("Invalidated all proxy cache entries");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate proxy cache");
            }
        }

        public async Task InvalidateProxyCacheAsync(string proxyId)
        {
            if (!_redisSettings.Enabled)
            {
                _logger.LogDebug("Redis caching is disabled, skipping cache invalidation");
                return;
            }

            try
            {
                var cacheKey = $"{_redisSettings.InstanceName}:proxies:{proxyId}";
                await _cache.RemoveAsync(cacheKey);

                _logger.LogInformation("Invalidated cache for proxy {ProxyId}", proxyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache for proxy {ProxyId}", proxyId);
            }
        }

        private async Task<StackExchange.Redis.IDatabase> GetRedisDatabaseAsync()
        {
            try
            {
                // Get the underlying Redis connection from the distributed cache
                // This is a bit of a hack, but necessary to access Redis-specific features
                var field = _cache.GetType().GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var redisCache = field.GetValue(_cache) as Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache;
                    if (redisCache != null)
                    {
                        var connectionField = redisCache.GetType().GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (connectionField != null)
                        {
                            var connection = connectionField.GetValue(redisCache) as StackExchange.Redis.IConnectionMultiplexer;
                            return connection?.GetDatabase();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get Redis database instance");
            }

            return null;
        }
    }
}
