using Slipka.DomainObjects;
using Slipka.Preprocessors.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Base
{
    /// <summary>
    /// Abstract base class for all preprocessors providing common functionality
    /// like ID generation, error handling, and lifecycle management.
    /// </summary>
    public abstract class AbstractPreprocessor : IPreprocessor
    {
        private readonly string _id;
        private bool _isEnabled;

        /// <summary>
        /// Initializes a new instance of the AbstractPreprocessor class
        /// </summary>
        protected AbstractPreprocessor()
        {
            _id = Guid.NewGuid().ToString();
            _isEnabled = true;
        }

        /// <summary>
        /// Initializes a new instance of the AbstractPreprocessor class with a specific ID
        /// </summary>
        /// <param name="id">The unique identifier for this preprocessor</param>
        protected AbstractPreprocessor(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _isEnabled = true;
        }

        /// <summary>
        /// Gets the unique identifier for this preprocessor instance
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets or sets whether this preprocessor is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Processes the HTTP request. This method provides error handling around the
        /// abstract ProcessRequestAsync method that subclasses must implement.
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ProcessAsync(HttpRequestMessage request, Session session)
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                await ProcessRequestAsync(request, session);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - failed preprocessing shouldn't break the request
                Console.WriteLine($"Preprocessor {Id} failed: {ex.Message}");
                // In a production system, you might want to use a proper logging framework
                // Logger.LogError(ex, "Preprocessor {PreprocessorId} failed to process request", Id);
            }
        }

        /// <summary>
        /// Abstract method that subclasses must implement to perform the actual request processing
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected abstract Task ProcessRequestAsync(HttpRequestMessage request, Session session);

        /// <summary>
        /// Helper method to safely add a header to the request, avoiding duplicates
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="name">The header name</param>
        /// <param name="value">The header value</param>
        protected void AddHeader(HttpRequestMessage request, string name, string value)
        {
            if (request.Headers.Contains(name))
            {
                request.Headers.Remove(name);
            }
            request.Headers.Add(name, value);
        }

        /// <summary>
        /// Helper method to check if a request matches certain criteria for preprocessing
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="uriPatterns">URI patterns to match against (regex)</param>
        /// <returns>True if the request should be processed by this preprocessor</returns>
        protected bool ShouldProcessRequest(HttpRequestMessage request, string[] uriPatterns)
        {
            if (uriPatterns == null || uriPatterns.Length == 0)
            {
                return true; // Process all requests if no patterns specified
            }

            var requestUri = request.RequestUri?.AbsolutePath ?? string.Empty;
            foreach (var pattern in uriPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(requestUri, pattern))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
