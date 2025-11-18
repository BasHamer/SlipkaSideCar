using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using MongoDB.Bson;
using Slipka.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    /// <summary>
    /// Health check for MongoDB database connectivity
    /// </summary>
    public class MongoHealthCheck : IHealthCheck
    {
        private readonly MongoSettings _mongoSettings;

        public MongoHealthCheck(MongoSettings mongoSettings)
        {
            _mongoSettings = mongoSettings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = new MongoClient(_mongoSettings.ConnectionString);

                // Test the connection by pinging the database
                var database = client.GetDatabase(_mongoSettings.Database);
                var pingCommand = new BsonDocument("ping", 1);
                await database.RunCommandAsync<BsonDocument>(pingCommand, cancellationToken: cancellationToken);

                // Optional: Get some basic stats to verify the database is operational
                var statsCommand = new BsonDocument("dbStats", 1);
                var stats = await database.RunCommandAsync<BsonDocument>(statsCommand, cancellationToken: cancellationToken);

                // Safely extract stats values that might be different types
                long collectionsCount = 0;
                if (stats.TryGetValue("collections", out var collectionsValue))
                {
                    collectionsCount = collectionsValue switch
                    {
                        BsonInt32 i32 => i32.Value,
                        BsonInt64 i64 => i64.Value,
                        BsonDouble d => (long)d.Value,
                        _ => 0
                    };
                }

                long storageSize = 0;
                if (stats.TryGetValue("storageSize", out var storageValue))
                {
                    storageSize = storageValue switch
                    {
                        BsonInt32 i32 => i32.Value,
                        BsonInt64 i64 => i64.Value,
                        BsonDouble d => (long)d.Value,
                        _ => 0
                    };
                }

                var healthData = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["database_name"] = _mongoSettings.Database,
                    ["connection_string"] = "***", // Hide sensitive info
                    ["collections_count"] = collectionsCount,
                    ["storage_size_mb"] = Math.Round(storageSize / (1024.0 * 1024.0), 2)
                };

                return HealthCheckResult.Healthy("MongoDB database is responding correctly", healthData);
            }
            catch (MongoConnectionException ex)
            {
                return HealthCheckResult.Unhealthy($"MongoDB connection failed: {ex.Message}", ex);
            }
            catch (MongoCommandException ex)
            {
                return HealthCheckResult.Unhealthy($"MongoDB command failed: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"MongoDB health check failed: {ex.Message}", ex);
            }
        }
    }
}
