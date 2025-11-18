using System.Threading.Tasks;

namespace Slipka.Caching
{
    /// <summary>
    /// Service for invalidating cached responses
    /// </summary>
    public interface ICacheInvalidationService
    {
        /// <summary>
        /// Invalidates all cache entries related to sessions
        /// </summary>
        Task InvalidateSessionCacheAsync();

        /// <summary>
        /// Invalidates cache entries for a specific session
        /// </summary>
        Task InvalidateSessionCacheAsync(string sessionId);

        /// <summary>
        /// Invalidates all cache entries related to proxies
        /// </summary>
        Task InvalidateProxyCacheAsync();

        /// <summary>
        /// Invalidates cache entries for a specific proxy
        /// </summary>
        Task InvalidateProxyCacheAsync(string proxyId);
    }
}
