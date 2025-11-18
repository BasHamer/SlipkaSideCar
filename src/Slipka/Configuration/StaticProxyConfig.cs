using Slipka.ApiArguments;
using Slipka.ValueObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Slipka.Configuration
{
    public class StaticProxyConfig
    {
        [Required]
        public string Id { get; set; }

        [Required]
        [Range(1, 65535)]
        public int Port { get; set; }

        [Required]
        public string TargetHost { get; set; }

        [Range(1, 65535)]
        public int? TargetPort { get; set; } = 80;

        public string Name { get; set; }

        public bool AutoStart { get; set; } = true;

        public string OpenFor { get; set; } = "8760:00:00"; // 1 year for static proxies

        public string RetainedFor { get; set; } = "365:00:00:00"; // 1 year for static proxies

        public int? MaxCallsInMemory { get; set; } // Maximum number of recent calls to keep in memory (null = unlimited)

        public List<CallTemplate> RecordedCalls { get; set; } = new List<CallTemplate>();

        public List<CallTemplate> InjectedCalls { get; set; } = new List<CallTemplate>();

        public List<CallTemplate> TaggedCalls { get; set; } = new List<CallTemplate>();

        public List<Header> Decorations { get; set; } = new List<Header>();

        public List<PreprocessorMessage> Preprocessors { get; set; } = new List<PreprocessorMessage>();

        public bool ProxyPortHttps { get; set; }

        public bool TargetPortHttps { get; set; }

        public StaticProxyConfig()
        {
            RecordedCalls = new List<CallTemplate>();
            InjectedCalls = new List<CallTemplate>();
            TaggedCalls = new List<CallTemplate>();
            Decorations = new List<Header>();
            Preprocessors = new List<PreprocessorMessage>();
        }
    }
}
