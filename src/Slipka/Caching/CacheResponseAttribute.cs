using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace Slipka.Caching
{
    /// <summary>
    /// Attribute to enable response caching for controller actions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CacheResponseAttribute : Attribute, IFilterFactory
    {
        private readonly TimeSpan _duration;
        private readonly string[] _varyByQueryKeys;
        private readonly string[] _varyByHeaders;

        public CacheResponseAttribute(int durationInSeconds, string[] varyByQueryKeys = null, string[] varyByHeaders = null)
        {
            _duration = TimeSpan.FromSeconds(durationInSeconds);
            _varyByQueryKeys = varyByQueryKeys ?? Array.Empty<string>();
            _varyByHeaders = varyByHeaders ?? Array.Empty<string>();
        }

        public bool IsReusable => true;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return new ResponseCacheFilter(_duration, _varyByQueryKeys, _varyByHeaders);
        }

        private class ResponseCacheFilter : IActionFilter
        {
            private readonly TimeSpan _duration;
            private readonly string[] _varyByQueryKeys;
            private readonly string[] _varyByHeaders;

            public ResponseCacheFilter(TimeSpan duration, string[] varyByQueryKeys, string[] varyByHeaders)
            {
                _duration = duration;
                _varyByQueryKeys = varyByQueryKeys;
                _varyByHeaders = varyByHeaders;
            }

            public void OnActionExecuting(ActionExecutingContext context)
            {
                // Generate cache key based on request
                var cacheKey = GenerateCacheKey(context);

                // Check if we have a distributed cache available
                var cache = context.HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache))
                    as Microsoft.Extensions.Caching.Distributed.IDistributedCache;

                if (cache != null)
                {
                    // Add cache key to HttpContext for potential use in result filter
                    context.HttpContext.Items["CacheKey"] = cacheKey;
                    context.HttpContext.Items["CacheDuration"] = _duration;
                }
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                // This could be enhanced to actually cache the response
                // For now, we rely on ASP.NET Core's built-in response caching
            }

            private string GenerateCacheKey(ActionExecutingContext context)
            {
                var request = context.HttpContext.Request;
                var keyParts = new System.Collections.Generic.List<string>
                {
                    request.Path.ToString()
                };

                // Add query parameters that should vary the cache
                if (_varyByQueryKeys.Any())
                {
                    foreach (var key in _varyByQueryKeys)
                    {
                        if (request.Query.TryGetValue(key, out var values))
                        {
                            keyParts.Add($"{key}={string.Join(",", values)}");
                        }
                    }
                }

                // Add headers that should vary the cache
                if (_varyByHeaders.Any())
                {
                    foreach (var header in _varyByHeaders)
                    {
                        if (request.Headers.TryGetValue(header, out var values))
                        {
                            keyParts.Add($"{header}={string.Join(",", values)}");
                        }
                    }
                }

                return string.Join("|", keyParts);
            }
        }
    }
}
