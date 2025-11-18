using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Slipka.ApiArguments;
using Slipka.Configuration;
using Slipka.DomainObjects;
using Slipka.Preprocessors.Interfaces;
using Slipka.Proxy;
using Slipka.Repositories;
using Slipka.ValueObjects;
using Slipka.Caching;

namespace Slipka.Controllers
{
    [Produces("application/json")]
    [Route("api/Proxies")]
    public class ProxiesController : Controller
    {
        public ProxiesController(
            ProxySettings settings,
            ProxyStore store,
            IFileRepository fileRepository,
            IMessageRepository messageRepository,
            StaticProxyManager staticProxyManager,
            ReverseProxyManager reverseProxyManager,
            ICacheInvalidationService cacheInvalidationService,
            IPreprocessorFactory preprocessorFactory)
        {
            Settings = settings;
            Store = store;
            FileRepository = fileRepository;
            MessageRepository = messageRepository;
            StaticProxyManager = staticProxyManager;
            ReverseProxyManager = reverseProxyManager;
            CacheInvalidationService = cacheInvalidationService;
            PreprocessorFactory = preprocessorFactory;
            Random = new Random(Guid.NewGuid().GetHashCode());
            ReservedPorts = Enumerable.Range(Settings.FirstPort, Settings.LastPort - Settings.FirstPort + 1).ToArray();
        }

        private ProxySettings Settings { get; }
        private ProxyStore Store { get; }
        private IFileRepository FileRepository { get; }
        private IMessageRepository MessageRepository { get; }
        private StaticProxyManager StaticProxyManager { get; }
        private ReverseProxyManager ReverseProxyManager { get; }
        private ICacheInvalidationService CacheInvalidationService { get; }
        private IPreprocessorFactory PreprocessorFactory { get; }
        private int[] ReservedPorts { get; }
        private Random Random { get; }

        // POST: api/Proxies
        [HttpPost]
        public async Task<ActionResult<Session>> Post([FromBody] CreateProxyMessage value)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var open = value.OpenFor == null ? Settings.DefaultOpenFor : TimeSpan.Parse(value.OpenFor);
            var retained = value.RetainedFor == null ? Settings.DefaultRetainedFor : TimeSpan.Parse(value.RetainedFor);

            var session = new Session
            {
                TargetHost = value.TargetHost,
                TargetPort = value.TargetPort ?? 80,
                ProxyPortHttps = value.ProxyPortHttps,
                TargetPortHttps = value.TargetPortHttps,
                LeaveProxyOpenUntil = DateTime.UtcNow.Add(open > Settings.MaxOpenFor ? Settings.MaxOpenFor : open),
                RetainDataUntil = DateTime.UtcNow.Add(retained > Settings.MaxRetainedFor ? Settings.MaxRetainedFor : retained),
                MaxCallsInMemory = value.MaxCallsInMemory
            };

            if (value.TaggedCalls != null)
                foreach (var item in value.TaggedCalls.Select(x => x.AsCallTemplate))
                    session.TaggedCalls.Add(item);
            if (value.RecordedCalls != null)
                foreach (var item in value.RecordedCalls.Select(x => x.AsCallTemplate))
                    session.RecordedCalls.Add(item);
            if (value.InjectedCalls != null)
                foreach (var item in value.InjectedCalls.Select(x => x.AsCallTemplate))
                    session.InjectedCalls.Add(item);
            if (value.Decorations != null)
                foreach (var item in value.Decorations.Select(x => x.AsHeader))
                    session.Decorations.Add(item);

            if (value.Preprocessors != null)
            {
                foreach (var preprocessorMessage in value.Preprocessors)
                {
                    try
                    {
                        var preprocessor = await preprocessorMessage.ToPreprocessorAsync(PreprocessorFactory);
                        session.Preprocessors.Add(preprocessor);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create preprocessor during proxy creation: {ex.Message}");
                        // Continue with other preprocessors even if one fails
                    }
                }
            }

            session.InternalId = new MongoDB.Bson.ObjectId();
            session.Calls = new ConcurrentQueue<Call>();

            Slipka.Proxy.Proxy proxy;
            lock (ReservedPorts)
            {
                var retries = 0;
                while (retries < 10)
                {
                    retries++;

                    session.ProxyPort = GetNewPort(session);
                    proxy = new Slipka.Proxy.Proxy(session, FileRepository, MessageRepository, Store.SaveSession);
                    try
                    {
                        proxy.Init(); // possibly not thread safe
                    }
                    catch (Exception e)
                    {
                        proxy.Dispose();
                        Console.WriteLine($"try {retries}, Failed to start proxy {e.Message}");
                        continue;
                    }
                    Store.Add(proxy);
                    return session;
                }
                throw new Exception($"Failed to find a port in {retries} tries");
            }
        }

        private int GetNewPort(Session value)
        {
            var staticProxyPorts = StaticProxyManager.GetStaticProxyStatuses().Select(s => s.Port);
            var available = ReservedPorts.Except(Store.All.Select(x => x.ProxyPort)).Except(staticProxyPorts).ToList();

            var retries = 0;
            while (retries < 1000)
            {
                retries++;
                using (TcpClient tcpClient = new TcpClient())
                {
                    // check if the port happens to be in use
                    if(available.Count() < 1)
                    {
                        Console.WriteLine("Fatal: ports exhausted");
                        throw new Exception("No available ports left for new proxies.");
                    }
                    var port = available.ElementAt(Random.Next(available.Count()));
                    try
                    {
                        
                        var result = tcpClient.BeginConnect("127.0.0.1", port, null, null);

                        var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000));

                        if (success)
                        {
                            available.RemoveAll(x => x == port);
                            tcpClient.EndConnect(result);
                            throw new Exception("in use");
                        }
                        return port;
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("Connection Refused", StringComparison.InvariantCultureIgnoreCase))
                            return port;
                        Console.WriteLine($"try {retries}, Port in use; available {available.Count} options message {e.Message}");
                    }
                }
            }
            throw new Exception($"Failed to start proxyt in {retries} tries");
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
            Store.Remove(id);
        }

        [HttpPut("{id}/record")]
        public ActionResult<Session> PutRecord(string id, [FromBody] CallTemplate call)
        {
            if (!SessionAvailableForModification(id, out var error, out Session session))
                return error;
            session.RecordedCalls.Add(call);
            return Ok(session);
        }

        [HttpPut("{id}/inject")]
        public ActionResult<Session> PutInject(string id, [FromBody] CallTemplate call)
        {
            if (!SessionAvailableForModification(id, out var error, out Session session))
                return error;
            if (string.IsNullOrWhiteSpace(call.StatusCode))
                call.StatusCode = HttpStatusCode.OK.ToString();
            session.InjectedCalls.Add(call);
            return Ok(session);
        }

        [HttpPut("{id}/tag")]
        public ActionResult<Session> PutTag(string id, [FromBody] CallTemplate call)
        {
            if (!SessionAvailableForModification(id, out var error, out Session session))
                return error;
            session.TaggedCalls.Add(call);
            return Ok(session);
        }

        [HttpPut("{id}/decorate")]
        public ActionResult<Session> PutDecorate(string id, [FromBody] Header header)
        {
            if (!SessionAvailableForModification(id, out var error, out Session session))
                return error;
            session.Decorations.Add(header);
            return Ok(session);
        }

        [HttpPut("{id}/preprocessor")]
        public async Task<ActionResult<Session>> PutPreprocessor(string id, [FromBody] PreprocessorMessage preprocessorMessage)
        {
            if (!SessionAvailableForModification(id, out var error, out Session session))
                return error;

            if (preprocessorMessage == null)
            {
                return BadRequest("Preprocessor configuration is required");
            }

            try
            {
                var preprocessor = await preprocessorMessage.ToPreprocessorAsync(PreprocessorFactory);
                session.Preprocessors.Add(preprocessor);
                return Ok(session);
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to create preprocessor: {ex.Message}");
            }
        }

        private bool SessionAvailableForModification(string id, out ActionResult<Session> error, out Session session)
        {
            try
            {
                session = Store[id].Session;
            }
            catch (KeyNotFoundException)
            {
                session = null;
                error = new StatusCodeResult((int)System.Net.HttpStatusCode.NotFound);
                return false;
            }
            if (session.Active)
            {
                error = new StatusCodeResult((int)System.Net.HttpStatusCode.Conflict);
                return false;
            }
            error = null;
            return true;
        }

        // GET: api/Proxies/static
        [HttpGet("static")]
        [ResponseCache(Duration = 30)] // Cache for 30 seconds
        public ActionResult<IEnumerable<Proxy.StaticProxyStatus>> GetStaticProxies()
        {
            var statuses = StaticProxyManager.GetStaticProxyStatuses();
            return Ok(statuses);
        }

        // GET: api/Proxies/static/{id}
        [HttpGet("static/{id}")]
        [ResponseCache(Duration = 30)] // Cache for 30 seconds
        public ActionResult<Proxy.StaticProxyStatus> GetStaticProxy(string id)
        {
            var statuses = StaticProxyManager.GetStaticProxyStatuses();
            var status = statuses.FirstOrDefault(s => s.Id == id);
            if (status == null)
            {
                return NotFound();
            }
            return Ok(status);
        }

        // POST: api/Proxies/static/{id}/start
        [HttpPost("static/{id}/start")]
        public async Task<ActionResult> StartStaticProxy(string id)
        {
            try
            {
                await StaticProxyManager.StartStaticProxyAsync(id);

                // Invalidate cache for this specific proxy since its status changed
                await CacheInvalidationService.InvalidateProxyCacheAsync(id);

                return Ok();
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to start static proxy: {ex.Message}");
            }
        }

        // POST: api/Proxies/static/{id}/stop
        [HttpPost("static/{id}/stop")]
        public async Task<ActionResult> StopStaticProxy(string id)
        {
            try
            {
                await StaticProxyManager.StopStaticProxyAsync(id);

                // Invalidate cache for this specific proxy since its status changed
                await CacheInvalidationService.InvalidateProxyCacheAsync(id);

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to stop static proxy: {ex.Message}");
            }
        }

        // POST: api/Proxies/static/{id}/restart
        [HttpPost("static/{id}/restart")]
        public async Task<ActionResult> RestartStaticProxy(string id)
        {
            try
            {
                await StaticProxyManager.RestartStaticProxyAsync(id);

                // Invalidate cache for this specific proxy since its status changed
                await CacheInvalidationService.InvalidateProxyCacheAsync(id);

                return Ok();
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to restart static proxy: {ex.Message}");
            }
        }

        // GET: api/Proxies/reverse
        [HttpGet("reverse")]
        [ResponseCache(Duration = 30)] // Cache for 30 seconds
        public ActionResult<ReverseProxyStatus> GetReverseProxy()
        {
            var status = new ReverseProxyStatus
            {
                Port = ReverseProxyManager.Port,
                IsRunning = ReverseProxyManager.IsRunning,
                Routes = ReverseProxyManager.Routes.ToList()
            };
            return Ok(status);
        }

        // POST: api/Proxies/reverse/start
        [HttpPost("reverse/start")]
        public async Task<ActionResult> StartReverseProxy()
        {
            try
            {
                await ReverseProxyManager.StartReverseProxyAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to start reverse proxy: {ex.Message}");
            }
        }

        // POST: api/Proxies/reverse/stop
        [HttpPost("reverse/stop")]
        public async Task<ActionResult> StopReverseProxy()
        {
            try
            {
                await ReverseProxyManager.StopReverseProxyAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to stop reverse proxy: {ex.Message}");
            }
        }
    }

    public class ReverseProxyStatus
    {
        public int Port { get; set; }
        public bool IsRunning { get; set; }
        public List<ReverseProxyRoute> Routes { get; set; }
    }
}
