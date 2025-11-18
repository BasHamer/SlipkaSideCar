using Slipka.ApiArguments;
using Slipka.DomainObjects;
using Slipka.Preprocessors;
using Slipka.Preprocessors.Implementations;
using Slipka.Preprocessors.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Slipka.Tests
{
    public class PreprocessorTests
    {
        [Fact]
        public async Task HeaderAuthenticationPreprocessor_AddsHeaderToRequest()
        {
            // Arrange
            var preprocessor = new HeaderAuthenticationPreprocessor("Authorization", "Bearer test-token");
            var session = new Session();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

            // Act
            await preprocessor.ProcessAsync(request, session);

            // Assert
            Assert.True(request.Headers.Contains("Authorization"));
            var headerValue = request.Headers.Authorization.ToString();
            Assert.Equal("Bearer test-token", headerValue);
        }

        [Fact]
        public async Task HeaderAuthenticationPreprocessor_WithUriPatterns_OnlyProcessesMatchingRequests()
        {
            // Arrange
            var preprocessor = new HeaderAuthenticationPreprocessor(
                "Authorization",
                "Bearer test-token",
                new[] { "/api/secure/.*" });
            var session = new Session();

            var matchingRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/api/secure/data");
            var nonMatchingRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/api/public/data");

            // Act
            await preprocessor.ProcessAsync(matchingRequest, session);
            await preprocessor.ProcessAsync(nonMatchingRequest, session);

            // Assert
            Assert.True(matchingRequest.Headers.Contains("Authorization"));
            Assert.False(nonMatchingRequest.Headers.Contains("Authorization"));
        }

        [Fact]
        public async Task PreprocessorMessage_CreatesHeaderAuthenticationPreprocessor()
        {
            // Arrange
            var factory = new PreprocessorFactory();
            var message = new PreprocessorMessage
            {
                Type = "HeaderAuthentication",
                Config = new Dictionary<string, object>
                {
                    ["headerName"] = "X-API-Key",
                    ["headerValue"] = "secret-key-123",
                    ["uriPatterns"] = new[] { "/api/v1/.*" }
                }
            };

            // Act
            var preprocessor = await message.ToPreprocessorAsync(factory) as HeaderAuthenticationPreprocessor;

            // Assert
            Assert.NotNull(preprocessor);
            Assert.Equal("X-API-Key", preprocessor.HeaderName);
            Assert.Equal("secret-key-123", preprocessor.HeaderValue);
            Assert.Equal(new[] { "/api/v1/.*" }, preprocessor.UriPatterns);
        }

        [Fact]
        public async Task PreprocessorMessage_CreatesSessionBasedPreprocessor()
        {
            // Arrange
            var factory = new PreprocessorFactory();
            var message = new PreprocessorMessage
            {
                Type = "SessionBased",
                Config = new Dictionary<string, object>
                {
                    ["loginUrl"] = "https://auth.example.com/token",
                    ["tokenExtractor"] = "json:access_token",
                    ["headerTemplate"] = "Bearer {token}",
                    ["sessionDuration"] = 3600
                }
            };

            // Act
            var preprocessor = await message.ToPreprocessorAsync(factory) as SessionBasedPreprocessor;

            // Assert
            Assert.NotNull(preprocessor);
            Assert.Equal("https://auth.example.com/token", preprocessor.LoginUrl);
            Assert.Equal("json:access_token", preprocessor.TokenExtractor);
            Assert.Equal("Bearer {token}", preprocessor.HeaderTemplate);
        }

        [Fact]
        public async Task PreprocessorMessage_UnknownType_ThrowsException()
        {
            // Arrange
            var factory = new PreprocessorFactory();
            var message = new PreprocessorMessage
            {
                Type = "UnknownType",
                Config = new Dictionary<string, object>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<Preprocessors.Interfaces.PreprocessorCreationException>(
                () => message.ToPreprocessorAsync(factory));
        }

        [Fact]
        public async Task PreprocessorMessage_MissingRequiredConfig_ThrowsException()
        {
            // Arrange
            var factory = new PreprocessorFactory();
            var message = new PreprocessorMessage
            {
                Type = "HeaderAuthentication",
                Config = new Dictionary<string, object>() // Missing required fields
            };

            // Act & Assert
            await Assert.ThrowsAsync<Preprocessors.Interfaces.PreprocessorCreationException>(
                () => message.ToPreprocessorAsync(factory));
        }

        [Fact]
        public void PreprocessorFactory_CanCreateBuiltInTypes()
        {
            // Arrange
            var factory = new PreprocessorFactory();

            // Act & Assert
            Assert.True(factory.CanCreatePreprocessor("HeaderAuthentication"));
            Assert.True(factory.CanCreatePreprocessor("headerauthentication"));
            Assert.True(factory.CanCreatePreprocessor("SessionBased"));
            Assert.True(factory.CanCreatePreprocessor("sessionbased"));
            Assert.False(factory.CanCreatePreprocessor("UnknownType"));
        }

        [Fact]
        public void PreprocessorFactory_GetSupportedTypes()
        {
            // Arrange
            var factory = new PreprocessorFactory();

            // Act
            var types = factory.GetSupportedTypes();

            // Assert
            Assert.Contains("headerauthentication", types);
            Assert.Contains("sessionbased", types);
        }

        [Fact]
        public void PreprocessorRegistry_CanRegisterCustomTypes()
        {
            // Arrange
            var registry = new PreprocessorRegistry();

            // Act
            registry.RegisterPreprocessorType("CustomType", typeof(HeaderAuthenticationPreprocessor));

            // Assert
            Assert.True(registry.IsPreprocessorTypeRegistered("CustomType"));
            Assert.Equal(typeof(HeaderAuthenticationPreprocessor), registry.GetPreprocessorType("CustomType"));
        }

        [Fact]
        public void PreprocessorRegistry_PreventsDuplicateRegistration()
        {
            // Arrange
            var registry = new PreprocessorRegistry();
            registry.RegisterPreprocessorType("CustomType", typeof(HeaderAuthenticationPreprocessor));

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                registry.RegisterPreprocessorType("CustomType", typeof(SessionBasedPreprocessor)));
        }

        [Fact]
        public void PreprocessorRegistry_CanUnregisterTypes()
        {
            // Arrange
            var registry = new PreprocessorRegistry();
            registry.RegisterPreprocessorType("CustomType", typeof(HeaderAuthenticationPreprocessor));

            // Act
            var result = registry.UnregisterPreprocessorType("CustomType");

            // Assert
            Assert.True(result);
            Assert.False(registry.IsPreprocessorTypeRegistered("CustomType"));
        }

        [Fact]
        public void Session_IncludesPreprocessorsCollection()
        {
            // Arrange & Act
            var session = new Session();

            // Assert
            Assert.NotNull(session.Preprocessors);
            Assert.Empty(session.Preprocessors);
        }

        [Fact]
        public void CreateProxyMessage_IncludesPreprocessors()
        {
            // Arrange & Act
            var message = new CreateProxyMessage();

            // Assert
            Assert.NotNull(message.Preprocessors);
            Assert.Empty(message.Preprocessors);
        }

        // Example custom preprocessor for testing
        public class CustomTestPreprocessor : Slipka.Preprocessors.Base.AbstractPreprocessor
        {
            public string CustomHeaderValue { get; }

            public CustomTestPreprocessor(string customHeaderValue)
            {
                CustomHeaderValue = customHeaderValue;
            }

            protected override Task ProcessRequestAsync(HttpRequestMessage request, DomainObjects.Session session)
            {
                AddHeader(request, "X-Custom-Test", CustomHeaderValue);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PreprocessorFactory_CanRegisterCustomPreprocessor()
        {
            // Arrange
            var factory = new PreprocessorFactory();

            // Register a custom preprocessor creator
            factory.RegisterPreprocessorType("CustomTest", async (message) =>
            {
                var config = message.Config;
                var headerValue = config.TryGetValue("headerValue", out var val) ? val?.ToString() : "default";
                return new CustomTestPreprocessor(headerValue);
            });

            var message = new PreprocessorMessage
            {
                Type = "CustomTest",
                Config = new Dictionary<string, object> { ["headerValue"] = "test-value" }
            };

            // Act
            var preprocessor = await message.ToPreprocessorAsync(factory) as CustomTestPreprocessor;

            // Assert
            Assert.NotNull(preprocessor);
            Assert.Equal("test-value", preprocessor.CustomHeaderValue);
            Assert.True(factory.CanCreatePreprocessor("CustomTest"));

            // Verify it can actually process a request
            var session = new Session();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");
            await preprocessor.ProcessAsync(request, session);

            Assert.True(request.Headers.Contains("X-Custom-Test"));
            Assert.Equal("test-value", request.Headers.GetValues("X-Custom-Test").First());
        }

        [Fact]
        public async Task CustomPreprocessor_ExecutesCorrectly()
        {
            // Arrange
            var preprocessor = new CustomTestPreprocessor("my-custom-value");
            var session = new Session();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

            // Act
            await preprocessor.ProcessAsync(request, session);

            // Assert
            Assert.True(request.Headers.Contains("X-Custom-Test"));
            Assert.Equal("my-custom-value", request.Headers.GetValues("X-Custom-Test").First());
        }
    }
}
