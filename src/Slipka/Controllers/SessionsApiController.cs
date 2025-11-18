using Microsoft.AspNetCore.Mvc;
using Slipka.Repositories;
using Slipka.DomainObjects;
using Slipka.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.Controllers
{
    [Produces("application/json")]
    [Route("api/SessionsApi")]
    public class SessionsApiController : Controller
    {
    public SessionsApiController(ISessionRepository sessionRepository, ICacheInvalidationService cacheInvalidationService)
    {
        SessionRepository = sessionRepository;
        CacheInvalidationService = cacheInvalidationService;
    }

    private ISessionRepository SessionRepository { get; }
    private ICacheInvalidationService CacheInvalidationService { get; }

        /// <summary>
        /// Get a single session by ID
        /// </summary>
        [HttpGet("{sessionId}")]
        [ResponseCache(Duration = 60)] // Cache for 60 seconds
        [ProducesResponseType(typeof(Session), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Session>> GetSession(string sessionId)
        {
            var session = await SessionRepository.GetSession(sessionId);
            if (session == null)
            {
                return NotFound();
            }
            return Ok(session);
        }

        /// <summary>
        /// Get all sessions, optionally filtered by tag
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "tag" })] // Cache for 30 seconds, vary by tag parameter
        [ProducesResponseType(typeof(IEnumerable<Session>), 200)]
        public async Task<ActionResult<IEnumerable<Session>>> GetSessions([FromQuery] string tag = null)
        {
            var sessions = await SessionRepository.GetAllSessions();
            if (tag != null)
            {
                sessions = sessions.Where(session => session.Tags.Contains(tag));
            }
            return Ok(sessions);
        }

        /// <summary>
        /// Get calls for a specific session with optional filtering
        /// </summary>
        [HttpGet("{sessionId}/calls")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "tag", "recorded", "minimumDuration" })] // Cache for 30 seconds, vary by filter parameters
        [ProducesResponseType(typeof(IEnumerable<Call>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IEnumerable<Call>>> GetSessionCalls(
            string sessionId,
            [FromQuery] string tag = null,
            [FromQuery] bool? recorded = null,
            [FromQuery] int? minimumDuration = null)
        {
            var session = await SessionRepository.GetSession(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            var calls = session.Calls.AsEnumerable();

            // Apply filters
            if (tag != null)
            {
                calls = calls.Where(call => call.Tags.Contains(tag));
            }

            if (recorded.HasValue)
            {
                calls = calls.Where(call => call.Recorded == recorded.Value);
            }

            if (minimumDuration.HasValue)
            {
                calls = calls.Where(call =>
                    call.Duration.HasValue && call.Duration.Value >= minimumDuration.Value);
            }

            return Ok(calls);
        }

        /// <summary>
        /// Delete a specific session
        /// </summary>
        [HttpDelete("{sessionId}")]
        [ProducesResponseType(typeof(bool), 200)]
        public async Task<ActionResult<bool>> DeleteSession(string sessionId)
        {
            var result = await SessionRepository.RemoveSession(sessionId);

            // Invalidate cache for all sessions since the list has changed
            await CacheInvalidationService.InvalidateSessionCacheAsync();

            return Ok(result);
        }

        /// <summary>
        /// Delete all sessions
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(typeof(bool), 200)]
        public async Task<ActionResult<bool>> DeleteAllSessions()
        {
            var result = await SessionRepository.RemoveSessions();

            // Invalidate cache for all sessions since all have been deleted
            await CacheInvalidationService.InvalidateSessionCacheAsync();

            return Ok(result);
        }
    }
}
