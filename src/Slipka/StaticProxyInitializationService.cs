using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slipka.Proxy;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    public class StaticProxyInitializationService : IHostedService
    {
        private readonly ILogger<StaticProxyInitializationService> _logger;
        private readonly StaticProxyManager _staticProxyManager;

        public StaticProxyInitializationService(
            ILogger<StaticProxyInitializationService> _logger,
            StaticProxyManager staticProxyManager)
        {
            this._logger = _logger;
            _staticProxyManager = staticProxyManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Static Proxy Initialization Service is starting");

            try
            {
                await _staticProxyManager.InitializeStaticProxiesAsync();
                _logger.LogInformation("Static Proxy Initialization Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize static proxies");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Static Proxy Initialization Service is stopping");
            return Task.CompletedTask;
        }
    }
}
