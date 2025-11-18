using Slipka.DomainObjects;
using Slipka.Preprocessors.Base;
using System.Net.Http;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Implementations
{
    /// <summary>
    /// A simple preprocessor that adds static authentication headers to requests.
    /// This is useful for APIs that require fixed API keys, bearer tokens, or other
    /// static authentication credentials.
    /// </summary>
    public class HeaderAuthenticationPreprocessor : AbstractPreprocessor
    {
        private readonly string _headerName;
        private readonly string _headerValue;
        private readonly string[] _uriPatterns;

        /// <summary>
        /// Initializes a new instance of the HeaderAuthenticationPreprocessor
        /// </summary>
        /// <param name="headerName">The name of the header to add (e.g., "Authorization")</param>
        /// <param name="headerValue">The value of the header (e.g., "Bearer token123")</param>
        /// <param name="uriPatterns">Optional URI patterns to match. If null or empty, applies to all requests</param>
        public HeaderAuthenticationPreprocessor(string headerName, string headerValue, string[] uriPatterns = null)
        {
            _headerName = headerName ?? throw new System.ArgumentNullException(nameof(headerName));
            _headerValue = headerValue ?? throw new System.ArgumentNullException(nameof(headerValue));
            _uriPatterns = uriPatterns;
        }

        /// <summary>
        /// Processes the request by adding the configured authentication header
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected override Task ProcessRequestAsync(HttpRequestMessage request, Session session)
        {
            // Check if this request should be processed based on URI patterns
            if (!ShouldProcessRequest(request, _uriPatterns))
            {
                return Task.CompletedTask;
            }

            // Add the authentication header
            AddHeader(request, _headerName, _headerValue);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the header name this preprocessor adds
        /// </summary>
        public string HeaderName => _headerName;

        /// <summary>
        /// Gets the header value this preprocessor adds
        /// </summary>
        public string HeaderValue => _headerValue;

        /// <summary>
        /// Gets the URI patterns this preprocessor matches
        /// </summary>
        public string[] UriPatterns => _uriPatterns;
    }
}
