using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slipka.Proxy;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    public class ReverseProxyInitializationService : IHostedService
    {
        private readonly ILogger<ReverseProxyInitializationService> _logger;
        private readonly ReverseProxyManager _reverseProxyManager;

        public ReverseProxyInitializationService(
            ILogger<ReverseProxyInitializationService> logger,
            ReverseProxyManager reverseProxyManager)
        {
            _logger = logger;
            _reverseProxyManager = reverseProxyManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Reverse Proxy Initialization Service is starting");

            try
            {
                await _reverseProxyManager.StartReverseProxyAsync();
                _logger.LogInformation("Reverse Proxy Initialization Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize reverse proxy");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Reverse Proxy Initialization Service is stopping");

            try
            {
                await _reverseProxyManager.StopReverseProxyAsync();
                _logger.LogInformation("Reverse Proxy stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop reverse proxy");
            }
        }
    }
}
