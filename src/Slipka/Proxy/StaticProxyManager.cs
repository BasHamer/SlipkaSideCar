using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Slipka.ApiArguments;
using Slipka.Configuration;
using Slipka.DomainObjects;
using Slipka.Preprocessors.Interfaces;
using Slipka.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.Proxy
{
    public class ReverseProxyManager
    {
        private readonly ILogger<ReverseProxyManager> _logger;
        private readonly ReverseProxySettings _settings;
        private readonly AuthenticationSettings _authSettings;
        private readonly IFileRepository _fileRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ProxyStore _proxyStore;
        private readonly IPreprocessorFactory _preprocessorFactory;

        private IWebHost _reverseProxyHost;
        private bool _isRunning;

        public ReverseProxyManager(
            ILogger<ReverseProxyManager> logger,
            ReverseProxySettings settings,
            AuthenticationSettings authSettings,
            IFileRepository fileRepository,
            IMessageRepository messageRepository,
            ISessionRepository sessionRepository,
            ProxyStore proxyStore,
            IPreprocessorFactory preprocessorFactory)
        {
            _logger = logger;
            _settings = settings;
            _authSettings = authSettings;
            _fileRepository = fileRepository;
            _messageRepository = messageRepository;
            _sessionRepository = sessionRepository;
            _proxyStore = proxyStore;
            _preprocessorFactory = preprocessorFactory;
        }

        public async Task StartReverseProxyAsync()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Reverse proxy is already running");
                return;
            }

            try
            {
                _reverseProxyHost = new WebHostBuilder()
                    .ConfigureServices(s =>
                    {
                        s.AddSingleton(_settings);
                        s.AddSingleton(_authSettings);
                        s.AddSingleton(_fileRepository);
                        s.AddSingleton(_messageRepository);
                        s.AddSingleton(_sessionRepository);
                        s.AddSingleton(_proxyStore);
                        s.AddSingleton(_preprocessorFactory);
                    })
                    .UseKestrel()
                    .UseUrls($"{(_settings.EnableHttps ? "https" : "http")}://*:{_settings.Port}")
                    .UseStartup<ReverseProxyStartup>()
                    .Build();

                await _reverseProxyHost.StartAsync();
                _isRunning = true;

                _logger.LogInformation("Started reverse proxy on port {Port}", _settings.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start reverse proxy on port {Port}", _settings.Port);
                throw;
            }
        }

        public async Task StopReverseProxyAsync()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Reverse proxy is not running");
                return;
            }

            try
            {
                await _reverseProxyHost.StopAsync();
                _reverseProxyHost.Dispose();
                _isRunning = false;

                _logger.LogInformation("Stopped reverse proxy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop reverse proxy", ex);
                throw;
            }
        }

        public bool IsRunning => _isRunning;
        public int Port => _settings.Port;
        public IEnumerable<ReverseProxyRoute> Routes => _settings.Routes;
    }

    public class StaticProxyManager
    {
        private readonly ILogger<StaticProxyManager> _logger;
        private readonly ProxySettings _proxySettings;
        private readonly StaticProxySettings _staticProxySettings;
        private readonly IFileRepository _fileRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ProxyStore _proxyStore;
        private readonly IPreprocessorFactory _preprocessorFactory;

        private readonly Dictionary<string, Proxy> _staticProxies;
        private readonly Dictionary<string, StaticProxyConfig> _staticProxyConfigs;

        public StaticProxyManager(
            ILogger<StaticProxyManager> logger,
            ProxySettings proxySettings,
            StaticProxySettings staticProxySettings,
            IFileRepository fileRepository,
            IMessageRepository messageRepository,
            ISessionRepository sessionRepository,
            ProxyStore proxyStore,
            IPreprocessorFactory preprocessorFactory)
        {
            _logger = logger;
            _proxySettings = proxySettings;
            _staticProxySettings = staticProxySettings;
            _fileRepository = fileRepository;
            _messageRepository = messageRepository;
            _sessionRepository = sessionRepository;
            _proxyStore = proxyStore;
            _preprocessorFactory = preprocessorFactory;

            _staticProxies = new Dictionary<string, Proxy>();
            _staticProxyConfigs = new Dictionary<string, StaticProxyConfig>();
        }

        public async Task InitializeStaticProxiesAsync()
        {
            _logger.LogInformation("Initializing static proxies...");

            foreach (var config in _staticProxySettings.Proxies)
            {
                _staticProxyConfigs[config.Id] = config;

                if (config.AutoStart)
                {
                    await StartStaticProxyAsync(config.Id);
                }
                else
                {
                    _logger.LogInformation("Static proxy '{ProxyId}' configured with AutoStart=false, skipping startup", config.Id);
                }
            }

            _logger.LogInformation("Static proxy initialization complete. Started {Count} proxies", _staticProxies.Count);
        }

        public async Task StartStaticProxyAsync(string proxyId)
        {
            if (!_staticProxyConfigs.TryGetValue(proxyId, out var config))
            {
                throw new ArgumentException($"Static proxy '{proxyId}' not found in configuration");
            }

            if (_staticProxies.ContainsKey(proxyId))
            {
                _logger.LogWarning("Static proxy '{ProxyId}' is already running", proxyId);
                return;
            }

            try
            {
                var session = await CreateSessionFromConfigAsync(config);
                var proxy = new Proxy(session, _fileRepository, _messageRepository, _proxyStore.SaveSession);

                proxy.Init();
                _staticProxies[proxyId] = proxy;
                _proxyStore.Add(proxy);

                _logger.LogInformation("Started static proxy '{ProxyId}' on port {Port} targeting {Host}:{TargetPort}",
                    proxyId, config.Port, config.TargetHost, config.TargetPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start static proxy '{ProxyId}'", proxyId);
                throw;
            }
        }

        public async Task StopStaticProxyAsync(string proxyId)
        {
            if (!_staticProxies.TryGetValue(proxyId, out var proxy))
            {
                _logger.LogWarning("Static proxy '{ProxyId}' is not running", proxyId);
                return;
            }

            try
            {
                _proxyStore.Remove(proxyId);
                _staticProxies.Remove(proxyId);

                _logger.LogInformation("Stopped static proxy '{ProxyId}'", proxyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop static proxy '{ProxyId}'", proxyId);
                throw;
            }
        }

        public async Task RestartStaticProxyAsync(string proxyId)
        {
            await StopStaticProxyAsync(proxyId);
            await StartStaticProxyAsync(proxyId);
        }

        public IEnumerable<StaticProxyStatus> GetStaticProxyStatuses()
        {
            return _staticProxyConfigs.Select(kvp =>
            {
                var config = kvp.Value;
                var isRunning = _staticProxies.ContainsKey(config.Id);
                var session = isRunning ? _staticProxies[config.Id].Session : null;
                var gotCalls = session?.Calls?.Any() ?? false;

                return new StaticProxyStatus
                {
                    Id = config.Id,
                    Name = config.Name,
                    Port = config.Port,
                    TargetHost = config.TargetHost,
                    TargetPort = config.TargetPort,
                    IsRunning = isRunning,
                    IsAutoStart = config.AutoStart,
                    CallCount = gotCalls? session.Calls.Count : 0,
                    LastActivity = gotCalls ? session.Calls.Max(c => c.Recieved) : null
                };
            });
        }

        public StaticProxyConfig GetStaticProxyConfig(string proxyId)
        {
            return _staticProxyConfigs.TryGetValue(proxyId, out var config) ? config : null;
        }

        public IEnumerable<string> GetStaticProxyIds()
        {
            return _staticProxyConfigs.Keys;
        }

        private async Task<Session> CreateSessionFromConfigAsync(StaticProxyConfig config)
        {
            var openFor = TimeSpan.Parse(config.OpenFor ?? "8760:00:00"); // Default to 1 year
            var retainedFor = TimeSpan.Parse(config.RetainedFor ?? "365:00:00:00"); // Default to 1 year

            var session = new Session
            {
                Id = config.Id,
                Name = config.Name ?? config.Id,
                TargetHost = config.TargetHost,
                TargetPort = config.TargetPort ?? 80,
                ProxyPort = config.Port,
                ProxyPortHttps = config.ProxyPortHttps,
                TargetPortHttps = config.TargetPortHttps,
                LeaveProxyOpenUntil = DateTime.UtcNow.Add(openFor),
                RetainDataUntil = DateTime.UtcNow.Add(retainedFor),
                MaxCallsInMemory = config.MaxCallsInMemory,
                InternalId = MongoDB.Bson.ObjectId.GenerateNewId(),
                Calls = new ConcurrentQueue<Call>(),
                Tags = new ConcurrentBag<string>(),
                RecordedCalls = [.. config.RecordedCalls ?? []],
                InjectedCalls = [.. config.InjectedCalls ?? []],
                TaggedCalls = [.. config.TaggedCalls ?? []],
                Decorations = [.. config.Decorations ?? []],
                Preprocessors = new ConcurrentBag<IPreprocessor>(await InitializePreprocessorsAsync(config.Preprocessors))
            };

            return session;
        }

        private async Task<List<IPreprocessor>> InitializePreprocessorsAsync(List<PreprocessorMessage> preprocessorMessages)
        {
            var preprocessors = new List<IPreprocessor>();

            if (preprocessorMessages == null || preprocessorMessages.Count == 0)
            {
                return preprocessors;
            }

            foreach (var message in preprocessorMessages)
            {
                try
                {
                    var preprocessor = await message.ToPreprocessorAsync(_preprocessorFactory);
                    preprocessors.Add(preprocessor);
                    _logger.LogInformation("Initialized preprocessor '{Type}' for static proxy", message.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize preprocessor of type '{Type}'", message.Type);
                    // Continue with other preprocessors even if one fails
                }
            }

            return preprocessors;
        }
    }

    public class StaticProxyStatus
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Port { get; set; }
        public string TargetHost { get; set; }
        public int? TargetPort { get; set; }
        public bool IsRunning { get; set; }
        public bool IsAutoStart { get; set; }
        public int CallCount { get; set; }
        public DateTime? LastActivity { get; set; }
    }
}
