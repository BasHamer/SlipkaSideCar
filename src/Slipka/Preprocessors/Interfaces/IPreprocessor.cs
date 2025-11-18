using Slipka.DomainObjects;
using System.Net.Http;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Interfaces
{
    /// <summary>
    /// Interface for request preprocessors that can modify HTTP requests before they are forwarded
    /// to the target server. Preprocessors enable authentication, session management, and other
    /// request transformations.
    /// </summary>
    public interface IPreprocessor
    {
        /// <summary>
        /// Unique identifier for this preprocessor instance
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Whether this preprocessor is enabled and should process requests
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Processes the HTTP request before it is forwarded to the target server.
        /// This method can modify headers, add authentication, establish sessions, etc.
        /// </summary>
        /// <param name="request">The HTTP request to process</param>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ProcessAsync(HttpRequestMessage request, Session session);
    }
}
