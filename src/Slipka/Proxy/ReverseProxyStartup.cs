using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Slipka.ApiArguments;
using Slipka.Configuration;
using Slipka.DomainObjects;
using Slipka.Repositories;
using Slipka.Preprocessors.Interfaces;
using Slipka.ValueObjects;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Slipka.Proxy
{
    public class ReverseProxyStartup
    {
        public ReverseProxyStartup(
            IConfiguration configuration,
            ReverseProxySettings settings,
            AuthenticationSettings authSettings,
            IFileRepository fileRepository,
            IMessageRepository messageRepository,
            ISessionRepository sessionRepository,
            ProxyStore proxyStore,
            IPreprocessorFactory preprocessorFactory)
        {
            Configuration = configuration;
            Settings = settings;
            AuthSettings = authSettings;
            FileRepository = fileRepository;
            MessageRepository = messageRepository;
            SessionRepository = sessionRepository;
            ProxyStore = proxyStore;
            PreprocessorFactory = preprocessorFactory;
            AuthValidator = new AuthenticationValidator(authSettings);
        }

        public IConfiguration Configuration { get; }
        public ReverseProxySettings Settings { get; }
        public AuthenticationSettings AuthSettings { get; }
        private IFileRepository FileRepository { get; }
        private IMessageRepository MessageRepository { get; }
        private ISessionRepository SessionRepository { get; }
        private ProxyStore ProxyStore { get; }
        private IPreprocessorFactory PreprocessorFactory { get; }
        private AuthenticationValidator AuthValidator { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddProxy(options =>
            {
                // Configure default proxy options for reverse proxy
                options.MessageHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env, ILogger<ReverseProxyStartup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("{**catchAll}", async context =>
                {
                    var route = FindMatchingRoute(context.Request.Path);
                    if (route == null)
                    {
                        logger.LogWarning("No route found for path: {Path}", context.Request.Path);
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("No route configured for this path");
                        return;
                    }

                    await ProxyToRoute(context, route, logger);
                });
            });
        }

        private ReverseProxyRoute FindMatchingRoute(PathString path)
        {
            // Simple path-based routing - find the first route where the path matches
            // More sophisticated routing could be implemented here (e.g., regex, priority-based)
            foreach (var route in Settings.Routes)
            {
                if (string.IsNullOrEmpty(route.Path))
                    continue;

                // Support wildcard matching
                if (route.Path.EndsWith("/*"))
                {
                    var basePath = route.Path.TrimEnd('*').TrimEnd('/');
                    if (path.StartsWithSegments(basePath, out _))
                    {
                        return route;
                    }
                }
                else if (route.Path == path.ToString())
                {
                    return route;
                }
            }

            return null;
        }

        private async Task ProxyToRoute(HttpContext context, ReverseProxyRoute route, ILogger logger)
        {
            try
            {
                // Create a session-like object for this request
                var session = await CreateSessionForRoute(route);

                // Create proxy handler for this route
                var loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
                var proxyHandlerLogger = loggerFactory.CreateLogger<ProxyHandler>();
                var handler = new ProxyHandler(session, FileRepository, MessageRepository, proxyHandlerLogger);

                // Build the target URI
                var targetUri = new UriBuilder
                {
                    Scheme = route.TargetHttps ? "https" : "http",
                    Host = route.TargetHost,
                    Port = route.TargetPort,
                    Path = context.Request.Path,
                    Query = context.Request.QueryString.ToString()
                }.Uri;

                // Create the forwarded request (copied from ProxyAdvancedExtensions)
                var requestMessage = CreateProxyHttpRequest(context, targetUri);

                try
                {
                    // Add route-specific headers
                    requestMessage.Headers.Add("X-Forwarded-Host", context.Request.Host.Host);
                    requestMessage.Headers.Add("X-Reverse-Proxy-Route", route.Id);

                    // Validate authentication if required
                    var authResult = AuthValidator.ValidateRequest(requestMessage, route.RequiresAuthentication);
                    if (!authResult.IsValid)
                    {
                        logger.LogWarning("Authentication failed for route {RouteId}: {ErrorMessage}", route.Id, authResult.ErrorMessage);
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        await context.Response.WriteAsync(authResult.ErrorMessage);
                        return;
                    }

                    // Add correlation ID header
                    string correlationId = null;
                    if (context.Request.Headers.TryGetValue("x-correlation-id", out var existingCorrelationId))
                    {
                        correlationId = existingCorrelationId.FirstOrDefault();
                    }

                    if (string.IsNullOrEmpty(correlationId))
                    {
                        correlationId = Guid.NewGuid().ToString();
                    }

                    requestMessage.Headers.Add("x-correlation-id", correlationId);

                    // Add correlation sub-ID header
                    string correlationSubId = null;
                    if (context.Request.Headers.TryGetValue("x-correlation-sub-id", out var existingCorrelationSubId))
                    {
                        correlationSubId = existingCorrelationSubId.FirstOrDefault();
                    }

                    if (string.IsNullOrEmpty(correlationSubId))
                    {
                        correlationSubId = Guid.NewGuid().ToString();
                    }

                    requestMessage.Headers.Add("x-correlation-sub-id", correlationSubId);

                    // Add decorations
                    if (route.Decorations != null)
                    {
                        foreach (var decoration in route.Decorations)
                        {
                            requestMessage.Headers.Add(decoration.Key, decoration.Values);
                        }
                    }

                    // Execute preprocessors
                    if (session.Preprocessors != null && session.Preprocessors.Count > 0)
                    {
                        var preprocessorTasks = session.Preprocessors
                            .Where(p => p.IsEnabled)
                            .Select(p => p.ProcessAsync(requestMessage, session));

                        await Task.WhenAll(preprocessorTasks);
                    }

                    // Send the request using our handler
                    using (var responseMessage = await handler.SendRequestAsync(requestMessage, context.RequestAborted))
                    {
                        // Apply error masking if configured
                        await ApplyErrorMaskingAsync(context, responseMessage, route, correlationId, correlationSubId, logger);

                        // Copy the response back to the client (copied from ProxyAdvancedExtensions)
                        await CopyProxyHttpResponse(context, responseMessage);
                    }
                }
                finally
                {
                    requestMessage.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to proxy request to route {RouteId}", route.Id);
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"Proxy error: {ex.Message}");
            }
        }

        private HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
        {
            var request = context.Request;

            var requestMessage = new HttpRequestMessage();
            var requestMethod = request.Method;
            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            // Copy the request headers
            foreach (var header in request.Headers)
            {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            return requestMessage;
        }

        private async Task ApplyErrorMaskingAsync(HttpContext context, HttpResponseMessage responseMessage, ReverseProxyRoute route, string correlationId, string correlationSubId, ILogger logger)
        {
            var errorMasking = route.ErrorMasking;
            if (!errorMasking.Enabled)
            {
                return;
            }

            // Check if this status code should be masked
            if (!errorMasking.ErrorStatusCodes.Contains((int)responseMessage.StatusCode))
            {
                return;
            }

            // Read the response content
            string responseContent = null;
            if (responseMessage.Content != null)
            {
                responseContent = await responseMessage.Content.ReadAsStringAsync();
            }

            bool shouldMask = false;

            // Check max length condition
            if (errorMasking.MaxErrorResponseLength.HasValue &&
                responseContent != null &&
                responseContent.Length > errorMasking.MaxErrorResponseLength.Value)
            {
                shouldMask = true;
            }

            // Check regex pattern condition
            if (!string.IsNullOrEmpty(errorMasking.ErrorResponseRegex) &&
                responseContent != null)
            {
                try
                {
                    var regex = new Regex(errorMasking.ErrorResponseRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (regex.IsMatch(responseContent))
                    {
                        shouldMask = true;
                    }
                }
                catch (RegexParseException ex)
                {
                    logger.LogWarning(ex, "Invalid regex pattern for error masking in route {RouteId}: {Pattern}", route.Id, errorMasking.ErrorResponseRegex);
                    // If regex is invalid, fall back to length-based masking
                    if (errorMasking.MaxErrorResponseLength.HasValue &&
                        responseContent != null &&
                        responseContent.Length > errorMasking.MaxErrorResponseLength.Value)
                    {
                        shouldMask = true;
                    }
                }
            }

            if (shouldMask)
            {
                // Log the full error with correlation IDs
                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("CorrelationSubId", correlationSubId))
                using (LogContext.PushProperty("RouteId", route.Id))
                using (LogContext.PushProperty("TargetHost", route.TargetHost))
                using (LogContext.PushProperty("TargetPort", route.TargetPort))
                using (LogContext.PushProperty("StatusCode", (int)responseMessage.StatusCode))
                {
                    logger.LogError("Masked error response from route {RouteId} to {TargetHost}:{TargetPort}. Full error content: {ErrorContent}",
                        route.Id, route.TargetHost, route.TargetPort, responseContent ?? "No content");
                }

                // Replace the response content with the masked message
                var maskedContent = $"{errorMasking.MaskedErrorMessage}\nCorrelation ID: {correlationId}\nCorrelation Sub ID: {correlationSubId}";
                responseMessage.Content = new StringContent(maskedContent, Encoding.UTF8, "text/plain");

                // Ensure correlation IDs are in response headers
                if (!responseMessage.Headers.Contains("x-correlation-id"))
                {
                    responseMessage.Headers.Add("x-correlation-id", correlationId);
                }
                if (!responseMessage.Headers.Contains("x-correlation-sub-id"))
                {
                    responseMessage.Headers.Add("x-correlation-sub-id", correlationSubId);
                }
            }
        }

        private async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
        {
            var response = context.Response;

            response.StatusCode = (int)responseMessage.StatusCode;
            foreach (var header in responseMessage.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
            response.Headers.Remove("transfer-encoding");

            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
            {
                const int StreamCopyBufferSize = 81920;
                await responseStream.CopyToAsync(response.Body, StreamCopyBufferSize, context.RequestAborted);
            }
        }

        private async Task<Session> CreateSessionForRoute(ReverseProxyRoute route)
        {
            var session = new Session
            {
                Id = $"reverse-proxy-{route.Id}",
                Name = $"Reverse Proxy Route: {route.Id}",
                TargetHost = route.TargetHost,
                TargetPort = route.TargetPort,
                TargetPortHttps = route.TargetHttps,
                ProxyPort = Settings.Port, // Use the reverse proxy port
                ProxyPortHttps = Settings.EnableHttps,
                LeaveProxyOpenUntil = DateTime.UtcNow.AddDays(365), // Long-lived for reverse proxy
                RetainDataUntil = DateTime.UtcNow.AddDays(365),
                MaxCallsInMemory = route.MaxCallsInMemory,
                InternalId = MongoDB.Bson.ObjectId.GenerateNewId(),
                Calls = new ConcurrentQueue<Call>(),
                Tags = new ConcurrentBag<string>(),
                RecordedCalls = new ConcurrentBag<Slipka.ValueObjects.CallTemplate>(route.RecordedCalls ?? new List<Slipka.ValueObjects.CallTemplate>()),
                InjectedCalls = new ConcurrentBag<Slipka.ValueObjects.CallTemplate>(route.InjectedCalls ?? new List<Slipka.ValueObjects.CallTemplate>()),
                TaggedCalls = new ConcurrentBag<Slipka.ValueObjects.CallTemplate>(),
                Decorations = new ConcurrentBag<Slipka.ValueObjects.Header>(route.Decorations ?? new List<Slipka.ValueObjects.Header>()),
                Preprocessors = new ConcurrentBag<IPreprocessor>(await InitializePreprocessorsAsync(route.Preprocessors))
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
                    var preprocessor = await message.ToPreprocessorAsync(PreprocessorFactory);
                    preprocessors.Add(preprocessor);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create preprocessor for reverse proxy route: {ex.Message}");
                    // Continue with other preprocessors even if one fails
                }
            }

            return preprocessors;
        }
    }
}
