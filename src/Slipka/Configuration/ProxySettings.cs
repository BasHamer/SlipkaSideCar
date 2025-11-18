using Slipka.Configuration;
using Slipka.ValueObjects;
using Slipka.ApiArguments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.Configuration
{
    [ConfigurationObject("ProxySettings")]
    public class ProxySettings
    {
        [ConfigurationMember("FirstPort")]
        public int FirstPort { get; set; }
        [ConfigurationMember("LastPort")]
        public int LastPort { get; set; }

        [ConfigurationMember("StaticFirstPort")]
        public int StaticFirstPort { get; set; }
        [ConfigurationMember("StaticLastPort")]
        public int StaticLastPort { get; set; }

        [ConfigurationMember("ReverseProxyPort")]
        public int ReverseProxyPort { get; set; } = 8080;

        [ConfigurationMember("DefaultOpenFor")]
        public TimeSpan DefaultOpenFor { get; set; }
        [ConfigurationMember("MaxOpenFor")]
        public TimeSpan MaxOpenFor { get; set; }

        [ConfigurationMember("DefaultRetainedFor")]
        public TimeSpan DefaultRetainedFor { get; set; }
        [ConfigurationMember("MaxRetainedFor")]
        public TimeSpan MaxRetainedFor { get; set; }
        [ConfigurationMember("GridFsCleanupLoop")]
        public int GridFsCleanupLoop { get; set; }
        [ConfigurationMember("ProxyPersistanceLoop")]
        public double ProxyPersistanceLoop { get; set; }
    }

    public class ReverseProxySettings
    {
        public int Port { get; set; } = 8080;
        public List<ReverseProxyRoute> Routes { get; set; } = new List<ReverseProxyRoute>();
        public bool EnableHttps { get; set; } = false;
    }

    public class ReverseProxyRoute
    {
        public string Id { get; set; }
        public string Path { get; set; } // e.g., "/api/*" or "/service1/*"
        public string TargetHost { get; set; } = "localhost";
        public int TargetPort { get; set; }
        public bool TargetHttps { get; set; } = false;
        public bool RequiresAuthentication { get; set; } = false;
        public List<Header> Decorations { get; set; } = new List<Header>();
        public List<CallTemplate> RecordedCalls { get; set; } = new List<CallTemplate>();
        public List<CallTemplate> InjectedCalls { get; set; } = new List<CallTemplate>();
        public List<PreprocessorMessage> Preprocessors { get; set; } = new List<PreprocessorMessage>();
        public ErrorMaskingSettings ErrorMasking { get; set; } = new ErrorMaskingSettings();
        public int? MaxCallsInMemory { get; set; } // Maximum number of recent calls to keep in memory (null = unlimited)
    }

    public class ErrorMaskingSettings
    {
        public bool Enabled { get; set; } = false;
        public int? MaxErrorResponseLength { get; set; } // Maximum length of error responses to mask (in characters)
        public string ErrorResponseRegex { get; set; } // Regex pattern to match error responses that should be masked
        public string MaskedErrorMessage { get; set; } = "An error occurred. Please check the logs for details using the correlation ID and correlation sub ID provided in the response headers.";
        public List<int> ErrorStatusCodes { get; set; } = new List<int> { 500, 502, 503, 504 }; // Status codes to apply masking to
    }
}
