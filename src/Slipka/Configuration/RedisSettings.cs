using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.Configuration
{
    [ConfigurationObject("SLIPKA")]
    public class RedisSettings
    {
        [ConfigurationMember("ConnectionString")]
        public string ConnectionString { get; set; } = "redis:6379";

        [ConfigurationMember("InstanceName")]
        public string InstanceName { get; set; } = "slipka";

        [ConfigurationMember("DefaultDatabase")]
        public int DefaultDatabase { get; set; } = 0;

        [ConfigurationMember("Enabled")]
        public bool Enabled { get; set; } = true;
    }
}
