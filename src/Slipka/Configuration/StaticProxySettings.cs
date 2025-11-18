using System.Collections.Generic;

namespace Slipka.Configuration
{
    [ConfigurationObject("StaticProxies")]
    public class StaticProxySettings
    {
        public List<StaticProxyConfig> Proxies { get; set; }

        public StaticProxySettings()
        {
            Proxies = new List<StaticProxyConfig>();
        }
    }
}
