using Slipka.Preprocessors.Base;
using Slipka.Preprocessors.Interfaces;
using System.Net.Http;
using System.Threading.Tasks;

namespace Slipka.CustomPreprocessors
{
    /// <summary>
    /// Example custom preprocessor demonstrating how to create user-defined preprocessors.
    /// This example shows a preprocessor that adds custom headers and modifies requests.
    /// </summary>
    [PreprocessorTypeName("CustomExample")]
    public class CustomExamplePreprocessor : AbstractPreprocessor
    {
        private readonly string _apiKey;
        private readonly string _environment;

        /// <summary>
        /// Initializes a new instance of the CustomExamplePreprocessor
        /// </summary>
        /// <param name="apiKey">The API key to include in requests</param>
        /// <param name="environment">The environment (dev, staging, prod)</param>
        public CustomExamplePreprocessor(string apiKey, string environment = "dev")
        {
            _apiKey = apiKey ?? throw new System.ArgumentNullException(nameof(apiKey));
            _environment = environment ?? "dev";
        }

        /// <summary>
        /// Processes the HTTP request by adding custom headers and modifications
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected override async Task ProcessRequestAsync(HttpRequestMessage request, DomainObjects.Session session)
        {
            // Add standard API key header
            AddHeader(request, "X-API-Key", _apiKey);

            // Add environment header
            AddHeader(request, "X-Environment", _environment);

            // Add timestamp header
            AddHeader(request, "X-Timestamp", System.DateTime.UtcNow.ToString("O"));

            // Add user agent if not present
            if (!request.Headers.Contains("User-Agent"))
            {
                AddHeader(request, "User-Agent", $"Slipka-Proxy/{_environment}");
            }

            // Custom logic based on request path
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.StartsWith("/api/admin"))
            {
                // Add admin-specific headers
                AddHeader(request, "X-Admin-Access", "true");

                // For admin endpoints, ensure we use POST method for safety
                if (request.Method == HttpMethod.Get)
                {
                    // You could modify the request here if needed
                    // request.Method = HttpMethod.Post;
                }
            }
            else if (path.StartsWith("/api/public"))
            {
                // Public endpoints get rate limiting headers
                AddHeader(request, "X-Rate-Limit", "1000");
            }

            // Simulate async work (e.g., token refresh, cache lookup, etc.)
            await Task.Delay(1); // Minimal delay to demonstrate async capability
        }
    }

    /// <summary>
    /// Another example preprocessor showing session-based authentication
    /// </summary>
    [PreprocessorTypeName("CustomSessionAuth")]
    public class CustomSessionAuthPreprocessor : AbstractPreprocessor, IStatefulPreprocessor
    {
        private readonly string _loginEndpoint;
        private readonly string _username;
        private readonly string _password;
        private string _sessionToken;
        private System.DateTime _tokenExpiry;

        public CustomSessionAuthPreprocessor(string loginEndpoint, string username, string password)
        {
            _loginEndpoint = loginEndpoint ?? throw new System.ArgumentNullException(nameof(loginEndpoint));
            _username = username ?? throw new System.ArgumentNullException(nameof(username));
            _password = password ?? throw new System.ArgumentNullException(nameof(password));
        }

        public bool IsInitialized => !string.IsNullOrEmpty(_sessionToken) && _tokenExpiry > System.DateTime.UtcNow;

        public async Task InitializeAsync(DomainObjects.Session session)
        {
            if (IsInitialized)
                return;

            // Perform login to get session token
            using var client = new HttpClient();
            var loginData = new System.Collections.Generic.Dictionary<string, string>
            {
                ["username"] = _username,
                ["password"] = _password
            };

            var content = new FormUrlEncodedContent(loginData);
            var response = await client.PostAsync(_loginEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                // Parse token from response (simplified example)
                _sessionToken = ExtractTokenFromResponse(responseContent);
                _tokenExpiry = System.DateTime.UtcNow.AddHours(1); // Token valid for 1 hour
            }
            else
            {
                throw new System.Exception($"Login failed: {response.StatusCode}");
            }
        }

        public Task CleanupAsync()
        {
            _sessionToken = null;
            _tokenExpiry = System.DateTime.MinValue;
            return Task.CompletedTask;
        }

        protected override async Task ProcessRequestAsync(HttpRequestMessage request, DomainObjects.Session session)
        {
            if (!IsInitialized)
            {
                await InitializeAsync(session);
            }

            AddHeader(request, "Authorization", $"Bearer {_sessionToken}");
            AddHeader(request, "X-Session-ID", session.Id);
        }

        private string ExtractTokenFromResponse(string responseContent)
        {
            // Simplified token extraction - in real scenarios, parse JSON properly
            var tokenStart = responseContent.IndexOf("\"token\":\"") + 9;
            if (tokenStart >= 9)
            {
                var tokenEnd = responseContent.IndexOf("\"", tokenStart);
                if (tokenEnd > tokenStart)
                {
                    return responseContent.Substring(tokenStart, tokenEnd - tokenStart);
                }
            }

            // Fallback - return the whole response if token parsing fails
            return responseContent;
        }
    }
}
