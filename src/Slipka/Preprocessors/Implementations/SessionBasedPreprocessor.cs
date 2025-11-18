using Slipka.DomainObjects;
using Slipka.Preprocessors.Base;
using Slipka.Preprocessors.Interfaces;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Implementations
{
    /// <summary>
    /// A stateful preprocessor that manages authentication sessions by performing login
    /// operations and maintaining session tokens. This preprocessor supports lazy loading
    /// - authentication only occurs when the API is first accessed.
    /// </summary>
    public class SessionBasedPreprocessor : AbstractPreprocessor, IStatefulPreprocessor
    {
        private readonly string _loginUrl;
        private readonly HttpMethod _loginMethod;
        private readonly string _loginBody;
        private readonly string _tokenExtractor;
        private readonly string _headerTemplate;
        private readonly TimeSpan _sessionDuration;
        private readonly string[] _uriPatterns;

        private bool _isInitialized;
        private string _sessionToken;
        private DateTime _tokenExpiry;
        private readonly object _initializationLock = new object();

        /// <summary>
        /// Initializes a new instance of the SessionBasedPreprocessor
        /// </summary>
        /// <param name="loginUrl">The URL to call for authentication</param>
        /// <param name="loginMethod">The HTTP method for the login request</param>
        /// <param name="loginBody">The request body for login</param>
        /// <param name="tokenExtractor">How to extract the token from the response (e.g., "json:access_token")</param>
        /// <param name="headerTemplate">Template for the auth header (e.g., "Bearer {token}")</param>
        /// <param name="sessionDuration">How long the session should last</param>
        /// <param name="uriPatterns">Optional URI patterns to match. If null or empty, applies to all requests</param>
        public SessionBasedPreprocessor(
            string loginUrl,
            HttpMethod loginMethod,
            string loginBody,
            string tokenExtractor,
            string headerTemplate,
            TimeSpan sessionDuration,
            string[] uriPatterns = null)
        {
            _loginUrl = loginUrl ?? throw new ArgumentNullException(nameof(loginUrl));
            _loginMethod = loginMethod ?? HttpMethod.Post;
            _loginBody = loginBody;
            _tokenExtractor = tokenExtractor ?? throw new ArgumentNullException(nameof(tokenExtractor));
            _headerTemplate = headerTemplate ?? "Bearer {token}";
            _sessionDuration = sessionDuration;
            _uriPatterns = uriPatterns;
        }

        /// <summary>
        /// Gets whether this preprocessor has been initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the authentication session by performing the login operation
        /// </summary>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous initialization</returns>
        public async Task InitializeAsync(Session session)
        {
            lock (_initializationLock)
            {
                if (_isInitialized && !IsTokenExpired())
                {
                    return;
                }
            }

            try
            {
                var token = await PerformLoginAsync(session);
                lock (_initializationLock)
                {
                    _sessionToken = token;
                    _tokenExpiry = DateTime.UtcNow.Add(_sessionDuration);
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize session preprocessor: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cleans up the preprocessor state
        /// </summary>
        /// <returns>A task representing the asynchronous cleanup</returns>
        public async Task CleanupAsync()
        {
            lock (_initializationLock)
            {
                _sessionToken = null;
                _tokenExpiry = DateTime.MinValue;
                _isInitialized = false;
            }

            await Task.CompletedTask; // Placeholder for potential cleanup operations
        }

        /// <summary>
        /// Processes the request by ensuring authentication and adding the session token
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected override async Task ProcessRequestAsync(HttpRequestMessage request, Session session)
        {
            // Check if this request should be processed based on URI patterns
            if (!ShouldProcessRequest(request, _uriPatterns))
            {
                return;
            }

            // Ensure we're initialized (lazy loading)
            if (!IsInitialized || IsTokenExpired())
            {
                await InitializeAsync(session);
            }

            // Add the authentication header with the session token
            var headerValue = _headerTemplate.Replace("{token}", _sessionToken);
            AddHeader(request, "Authorization", headerValue);
        }

        /// <summary>
        /// Performs the actual login operation to obtain an authentication token
        /// </summary>
        /// <param name="session">The proxy session context</param>
        /// <returns>The authentication token</returns>
        private async Task<string> PerformLoginAsync(Session session)
        {
            using var client = new HttpClient();

            var loginRequest = new HttpRequestMessage(_loginMethod, _loginUrl);

            if (!string.IsNullOrEmpty(_loginBody))
            {
                loginRequest.Content = new StringContent(_loginBody, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(loginRequest);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Login failed with status {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return ExtractToken(responseContent);
        }

        /// <summary>
        /// Extracts the authentication token from the login response
        /// </summary>
        /// <param name="responseContent">The response content from the login request</param>
        /// <returns>The extracted token</returns>
        private string ExtractToken(string responseContent)
        {
            if (string.IsNullOrEmpty(_tokenExtractor))
            {
                throw new InvalidOperationException("Token extractor is not configured");
            }

            var parts = _tokenExtractor.Split(':');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException("Invalid token extractor format. Expected 'type:selector'");
            }

            var type = parts[0].ToLower();
            var selector = parts[1];

            switch (type)
            {
                case "json":
                    return ExtractFromJson(responseContent, selector);
                case "header":
                    // For future implementation - extract from response headers
                    throw new NotImplementedException("Header token extraction not yet implemented");
                default:
                    throw new InvalidOperationException($"Unknown token extractor type: {type}");
            }
        }

        /// <summary>
        /// Extracts a token from JSON response content
        /// </summary>
        /// <param name="jsonContent">The JSON response content</param>
        /// <param name="propertyPath">The property path to the token (e.g., "access_token" or "data.token")</param>
        /// <returns>The extracted token</returns>
        private string ExtractFromJson(string jsonContent, string propertyPath)
        {
            try
            {
                var json = JsonDocument.Parse(jsonContent).RootElement;

                // Support nested properties with dot notation (e.g., "data.token")
                var properties = propertyPath.Split('.');
                var currentElement = json;

                foreach (var property in properties)
                {
                    if (currentElement.ValueKind == JsonValueKind.Object && currentElement.TryGetProperty(property, out var childElement))
                    {
                        currentElement = childElement;
                    }
                    else
                    {
                        throw new Exception($"Property '{property}' not found in JSON response");
                    }
                }

                if (currentElement.ValueKind == JsonValueKind.String)
                {
                    return currentElement.GetString();
                }
                else
                {
                    throw new Exception($"Property '{propertyPath}' is not a string value");
                }
            }
            catch (JsonException ex)
            {
                throw new Exception($"Failed to parse JSON response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if the current session token has expired
        /// </summary>
        /// <returns>True if the token has expired</returns>
        private bool IsTokenExpired()
        {
            return DateTime.UtcNow >= _tokenExpiry;
        }

        // Public properties for inspection/configuration
        public string LoginUrl => _loginUrl;
        public HttpMethod LoginMethod => _loginMethod;
        public string LoginBody => _loginBody;
        public string TokenExtractor => _tokenExtractor;
        public string HeaderTemplate => _headerTemplate;
        public TimeSpan SessionDuration => _sessionDuration;
        public string[] UriPatterns => _uriPatterns;
    }
}
