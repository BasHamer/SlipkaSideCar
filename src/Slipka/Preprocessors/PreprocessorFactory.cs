using Slipka.ApiArguments;
using Slipka.Preprocessors.Implementations;
using Slipka.Preprocessors.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Slipka.Preprocessors
{
    /// <summary>
    /// Default implementation of IPreprocessorFactory that supports built-in preprocessor types.
    /// This factory can be extended to support custom preprocessor types through registration.
    /// </summary>
    public class PreprocessorFactory : IPreprocessorFactory
    {
        private readonly Dictionary<string, Func<PreprocessorMessage, Task<IPreprocessor>>> _preprocessorCreators;

        /// <summary>
        /// Initializes a new instance of the PreprocessorFactory with built-in preprocessor types.
        /// </summary>
        public PreprocessorFactory()
        {
            _preprocessorCreators = new Dictionary<string, Func<PreprocessorMessage, Task<IPreprocessor>>>
            {
                ["headerauthentication"] = CreateHeaderAuthenticationPreprocessorAsync,
                ["headerauth"] = CreateHeaderAuthenticationPreprocessorAsync,
                ["sessionbased"] = CreateSessionBasedPreprocessorAsync,
                ["sessionauth"] = CreateSessionBasedPreprocessorAsync
            };
        }

        /// <summary>
        /// Creates a preprocessor instance from the provided configuration message.
        /// </summary>
        /// <param name="message">The preprocessor configuration message</param>
        /// <returns>The configured preprocessor instance</returns>
        /// <exception cref="PreprocessorCreationException">Thrown when the preprocessor cannot be created</exception>
        public async Task<IPreprocessor> CreatePreprocessorAsync(PreprocessorMessage message)
        {
            if (message == null)
            {
                throw new PreprocessorCreationException("Preprocessor message cannot be null");
            }

            if (string.IsNullOrEmpty(message.Type))
            {
                throw new PreprocessorCreationException("Preprocessor type must be specified");
            }

            var typeKey = message.Type.ToLower();
            if (!_preprocessorCreators.TryGetValue(typeKey, out var creator))
            {
                throw new PreprocessorCreationException($"Unknown preprocessor type: {message.Type}");
            }

            try
            {
                return await creator(message);
            }
            catch (Exception ex)
            {
                throw new PreprocessorCreationException($"Failed to create preprocessor of type '{message.Type}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if the factory can create a preprocessor of the specified type.
        /// </summary>
        /// <param name="preprocessorType">The preprocessor type to check</param>
        /// <returns>True if the factory supports this type, false otherwise</returns>
        public bool CanCreatePreprocessor(string preprocessorType)
        {
            if (string.IsNullOrEmpty(preprocessorType))
            {
                return false;
            }

            return _preprocessorCreators.ContainsKey(preprocessorType.ToLower());
        }

        /// <summary>
        /// Gets all preprocessor types supported by this factory.
        /// </summary>
        /// <returns>Array of supported preprocessor type names</returns>
        public string[] GetSupportedTypes()
        {
            return new List<string>(_preprocessorCreators.Keys).ToArray();
        }

        /// <summary>
        /// Registers a custom preprocessor creator function.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <param name="creator">The function that creates the preprocessor</param>
        /// <exception cref="ArgumentException">Thrown when the type is already registered</exception>
        public void RegisterPreprocessorType(string typeName, Func<PreprocessorMessage, Task<IPreprocessor>> creator)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));
            }

            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }

            var typeKey = typeName.ToLower();
            if (_preprocessorCreators.ContainsKey(typeKey))
            {
                throw new ArgumentException($"Preprocessor type '{typeName}' is already registered", nameof(typeName));
            }

            _preprocessorCreators[typeKey] = creator;
        }

        /// <summary>
        /// Unregisters a custom preprocessor type.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type to unregister</param>
        /// <returns>True if the type was unregistered, false if it wasn't found</returns>
        public bool UnregisterPreprocessorType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var typeKey = typeName.ToLower();

            // Don't allow unregistering built-in types
            if (IsBuiltInType(typeKey))
            {
                return false;
            }

            return _preprocessorCreators.Remove(typeKey);
        }

        private bool IsBuiltInType(string typeKey)
        {
            return typeKey == "headerauthentication" ||
                   typeKey == "headerauth" ||
                   typeKey == "sessionbased" ||
                   typeKey == "sessionauth";
        }

        private async Task<IPreprocessor> CreateHeaderAuthenticationPreprocessorAsync(PreprocessorMessage message)
        {
            if (message.Config == null)
            {
                throw new PreprocessorCreationException("Config is required for HeaderAuthentication preprocessor");
            }

            if (!message.Config.TryGetValue("headerName", out var headerNameObj) || !(headerNameObj is string headerName))
            {
                throw new PreprocessorCreationException("headerName is required and must be a string");
            }

            if (!message.Config.TryGetValue("headerValue", out var headerValueObj) || !(headerValueObj is string headerValue))
            {
                throw new PreprocessorCreationException("headerValue is required and must be a string");
            }

            string[] uriPatterns = null;
            if (message.Config.TryGetValue("uriPatterns", out var uriPatternsObj))
            {
                if (uriPatternsObj is string[] patterns)
                {
                    uriPatterns = patterns;
                }
                else if (uriPatternsObj is List<object> patternList)
                {
                    uriPatterns = patternList.ConvertAll(p => p?.ToString()).ToArray();
                }
            }

            return new HeaderAuthenticationPreprocessor(headerName, headerValue, uriPatterns);
        }

        private async Task<IPreprocessor> CreateSessionBasedPreprocessorAsync(PreprocessorMessage message)
        {
            if (message.Config == null)
            {
                throw new PreprocessorCreationException("Config is required for SessionBased preprocessor");
            }

            // Required parameters
            if (!message.Config.TryGetValue("loginUrl", out var loginUrlObj) || !(loginUrlObj is string loginUrl))
            {
                throw new PreprocessorCreationException("loginUrl is required and must be a string");
            }

            if (!message.Config.TryGetValue("tokenExtractor", out var tokenExtractorObj) || !(tokenExtractorObj is string tokenExtractor))
            {
                throw new PreprocessorCreationException("tokenExtractor is required and must be a string");
            }

            // Optional parameters with defaults
            var loginMethod = HttpMethod.Post;
            if (message.Config.TryGetValue("loginMethod", out var loginMethodObj) && loginMethodObj is string methodStr)
            {
                loginMethod = new HttpMethod(methodStr);
            }

            var loginBody = message.Config.TryGetValue("loginBody", out var loginBodyObj) ? loginBodyObj?.ToString() : null;

            var headerTemplate = "Bearer {token}";
            if (message.Config.TryGetValue("headerTemplate", out var headerTemplateObj) && headerTemplateObj is string template)
            {
                headerTemplate = template;
            }

            var sessionDuration = TimeSpan.FromHours(1); // Default 1 hour
            if (message.Config.TryGetValue("sessionDuration", out var durationObj))
            {
                if (durationObj is int seconds)
                {
                    sessionDuration = TimeSpan.FromSeconds(seconds);
                }
                else if (durationObj is string durationStr)
                {
                    if (int.TryParse(durationStr, out var parsedSeconds))
                    {
                        sessionDuration = TimeSpan.FromSeconds(parsedSeconds);
                    }
                    else
                    {
                        throw new PreprocessorCreationException("sessionDuration must be a valid integer (seconds)");
                    }
                }
            }

            string[] uriPatterns = null;
            if (message.Config.TryGetValue("uriPatterns", out var uriPatternsObj))
            {
                if (uriPatternsObj is string[] patterns)
                {
                    uriPatterns = patterns;
                }
                else if (uriPatternsObj is List<object> patternList)
                {
                    uriPatterns = patternList.ConvertAll(p => p?.ToString()).ToArray();
                }
            }

            return new SessionBasedPreprocessor(
                loginUrl,
                loginMethod,
                loginBody,
                tokenExtractor,
                headerTemplate,
                sessionDuration,
                uriPatterns);
        }
    }
}
