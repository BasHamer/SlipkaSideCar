using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Slipka.Proxy;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    public class StaticProxyHealthCheck : IHealthCheck
    {
        private readonly ILogger<StaticProxyHealthCheck> _logger;
        private readonly StaticProxyManager _staticProxyManager;
        private readonly ProxyStore _proxyStore;

        public StaticProxyHealthCheck(
            ILogger<StaticProxyHealthCheck> logger,
            StaticProxyManager staticProxyManager,
            ProxyStore proxyStore)
        {
            _logger = logger;
            _staticProxyManager = staticProxyManager;
            _proxyStore = proxyStore;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var staticProxyStatuses = _staticProxyManager.GetStaticProxyStatuses().ToList();
                var autoStartProxies = staticProxyStatuses.Where(s => s.IsAutoStart).ToList();
                var runningAutoStartProxies = autoStartProxies.Where(s => s.IsRunning).ToList();

                // Get static proxy IDs to exclude from dynamic proxies
                var staticProxyIds = _staticProxyManager.GetStaticProxyIds().ToHashSet();

                // Get dynamic proxy information (exclude static proxies)
                var dynamicProxies = _proxyStore.All.Where(s => !staticProxyIds.Contains(s.Id)).ToList();

                // Create detailed static proxy information
                var staticProxyDetails = staticProxyStatuses.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    port = s.Port,
                    isRunning = s.IsRunning,
                    isAutoStart = s.IsAutoStart,
                    targetHost = s.TargetHost,
                    targetPort = s.TargetPort,
                    callCount = s.CallCount,
                    lastActivity = s.LastActivity
                }).ToList();

                // Create detailed dynamic proxy information
                var dynamicProxyDetails = dynamicProxies.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    proxyPort = p.ProxyPort,
                    targetHost = p.TargetHost,
                    targetPort = p.TargetPort,
                    proxyPortHttps = p.ProxyPortHttps,
                    targetPortHttps = p.TargetPortHttps,
                    isActive = p.Active,
                    createdAt = p.InternalId.CreationTime,
                    leaveProxyOpenUntil = p.LeaveProxyOpenUntil,
                    retainDataUntil = p.RetainDataUntil,
                    callCount = p.Calls.Count,
                    tags = p.Tags,
                    hasPreprocessors = p.Preprocessors.Any()
                }).ToList();

                var healthData = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["total_static_proxies"] = staticProxyStatuses.Count,
                    ["auto_start_proxies"] = autoStartProxies.Count,
                    ["running_auto_start_proxies"] = runningAutoStartProxies.Count,
                    ["running_static_proxies"] = staticProxyStatuses.Count(s => s.IsRunning),
                    ["total_dynamic_proxies"] = dynamicProxies.Count,
                    ["active_dynamic_proxies"] = dynamicProxies.Count(p => p.Active),
                    ["static_proxies"] = staticProxyDetails,
                    ["dynamic_proxies"] = dynamicProxyDetails
                };

                // Check if all auto-start proxies are running
                if (runningAutoStartProxies.Count < autoStartProxies.Count)
                {
                    var failedProxies = autoStartProxies.Where(s => !s.IsRunning).Select(s => s.Id);
                    var description = $"Some auto-start proxies are not running: {string.Join(", ", failedProxies)}";

                    _logger.LogWarning("Static proxy health check failed: {Description}", description);

                    return HealthCheckResult.Degraded(description, data: healthData);
                }

                // Check port availability for running proxies
                foreach (var status in staticProxyStatuses.Where(s => s.IsRunning))
                {
                    if (!await IsPortOpenAsync("127.0.0.1", status.Port, cancellationToken))
                    {
                        var description = $"Static proxy '{status.Id}' port {status.Port} is not accessible";
                        _logger.LogWarning("Static proxy health check failed: {Description}", description);

                        return HealthCheckResult.Unhealthy(description, data: healthData);
                    }
                }

                var healthyDescription = $"All {runningAutoStartProxies.Count} auto-start static proxies are running and healthy";
                _logger.LogDebug("Static proxy health check passed: {Description}", healthyDescription);

                return HealthCheckResult.Healthy(healthyDescription, healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Static proxy health check failed with exception");
                return HealthCheckResult.Unhealthy("Static proxy health check failed", ex);
            }
        }

        private async Task<bool> IsPortOpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(5000, cancellationToken); // 5 second timeout

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        return false; // Timeout
                    }

                    return client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
