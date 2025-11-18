using Slipka.DomainObjects;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Interfaces
{
    /// <summary>
    /// Interface for stateful preprocessors that maintain session state and require
    /// initialization/cleanup. These preprocessors can perform lazy loading of authentication
    /// tokens and manage complex session lifecycles.
    /// </summary>
    public interface IStatefulPreprocessor : IPreprocessor
    {
        /// <summary>
        /// Whether this preprocessor has been initialized and is ready to process requests
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes the preprocessor state. This is called lazily when the first
        /// request requiring this preprocessor is processed.
        /// </summary>
        /// <param name="session">The proxy session context</param>
        /// <returns>A task representing the asynchronous initialization</returns>
        Task InitializeAsync(Session session);

        /// <summary>
        /// Cleans up the preprocessor state when the session ends or preprocessor is disabled
        /// </summary>
        /// <returns>A task representing the asynchronous cleanup</returns>
        Task CleanupAsync();
    }
}
