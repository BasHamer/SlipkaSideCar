using Slipka.Configuration;
using System.Collections.Generic;
using Xunit;

namespace Slipka.Tests
{
    public class StaticProxyValidatorTests
    {
        private readonly ProxySettings _proxySettings;

        public StaticProxyValidatorTests()
        {
            _proxySettings = new ProxySettings
            {
                FirstPort = 61000,
                LastPort = 61500,
                StaticFirstPort = 61501,
                StaticLastPort = 62000
            };
        }

        [Fact]
        public void Validate_ValidConfiguration_ReturnsValidResult()
        {
            // Arrange
            var validator = new StaticProxyValidator(_proxySettings);
            var settings = new StaticProxySettings
            {
                Proxies = new List<StaticProxyConfig>
                {
                    new StaticProxyConfig
                    {
                        Id = "test-proxy-1",
                        Port = 61501,
                        TargetHost = "api.example.com",
                        TargetPort = 80,
                        AutoStart = true
                    },
                    new StaticProxyConfig
                    {
                        Id = "test-proxy-2",
                        Port = 61502,
                        TargetHost = "api2.example.com",
                        TargetPort = 443,
                        AutoStart = false
                    }
                }
            };

            // Act
            var result = validator.Validate(settings);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_PortOutsideRange_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new StaticProxyValidator(_proxySettings);
            var settings = new StaticProxySettings
            {
                Proxies = new List<StaticProxyConfig>
                {
                    new StaticProxyConfig
                    {
                        Id = "invalid-proxy",
                        Port = 60000, // Outside range
                        TargetHost = "api.example.com"
                    }
                }
            };

            // Act
            var result = validator.Validate(settings);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("outside allowed static proxy range"));
        }

        [Fact]
        public void Validate_DuplicatePorts_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new StaticProxyValidator(_proxySettings);
            var settings = new StaticProxySettings
            {
                Proxies = new List<StaticProxyConfig>
                {
                    new StaticProxyConfig
                    {
                        Id = "proxy-1",
                        Port = 61501,
                        TargetHost = "api1.example.com"
                    },
                    new StaticProxyConfig
                    {
                        Id = "proxy-2",
                        Port = 61501, // Duplicate port
                        TargetHost = "api2.example.com"
                    }
                }
            };

            // Act
            var result = validator.Validate(settings);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Duplicate port"));
        }

        [Fact]
        public void Validate_DuplicateIds_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new StaticProxyValidator(_proxySettings);
            var settings = new StaticProxySettings
            {
                Proxies = new List<StaticProxyConfig>
                {
                    new StaticProxyConfig
                    {
                        Id = "duplicate-id",
                        Port = 61501,
                        TargetHost = "api1.example.com"
                    },
                    new StaticProxyConfig
                    {
                        Id = "duplicate-id", // Duplicate ID
                        Port = 61502,
                        TargetHost = "api2.example.com"
                    }
                }
            };

            // Act
            var result = validator.Validate(settings);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Duplicate proxy ID"));
        }
    }
}
