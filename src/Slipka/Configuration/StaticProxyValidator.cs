using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Slipka.Configuration
{
    public class StaticProxyValidator
    {
        private readonly ProxySettings _proxySettings;

        public StaticProxyValidator(ProxySettings proxySettings)
        {
            _proxySettings = proxySettings;
        }

        public ValidationResult Validate(StaticProxySettings staticProxySettings)
        {
            var result = new ValidationResult();
            var usedPorts = new HashSet<int>();

            foreach (var proxy in staticProxySettings.Proxies)
            {
                // Check for duplicate IDs
                if (staticProxySettings.Proxies.Count(p => p.Id == proxy.Id) > 1)
                {
                    result.Errors.Add($"Duplicate proxy ID: {proxy.Id}");
                    continue;
                }

                // Check port range
                if (proxy.Port < _proxySettings.FirstPort || proxy.Port > _proxySettings.LastPort)
                {
                    result.Errors.Add($"Proxy '{proxy.Id}' port {proxy.Port} is outside allowed range [{_proxySettings.FirstPort}, {_proxySettings.LastPort}]");
                }

                // Check for duplicate ports within static proxies
                if (!usedPorts.Add(proxy.Port))
                {
                    result.Errors.Add($"Duplicate port {proxy.Port} used by proxy '{proxy.Id}'");
                }

                // Check if port is currently in use
                if (IsPortInUse(proxy.Port))
                {
                    result.Warnings.Add($"Port {proxy.Port} for proxy '{proxy.Id}' is currently in use");
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var result = tcpClient.BeginConnect("127.0.0.1", port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000));

                    if (success)
                    {
                        tcpClient.EndConnect(result);
                        return true;
                    }
                }
            }
            catch
            {
                // Connection refused means port is free
                return false;
            }

            return false;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }

        public ValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }
}
