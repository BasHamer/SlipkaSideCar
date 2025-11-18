using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Slipka.Caching;
using Slipka.Configuration;
using Slipka.Repositories;
using Slipka.Proxy;
using System.IO;

namespace Slipka
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var status = new Status();
            var configurationFactory = new ConfigurationFactory(Configuration, status);
            var settings = configurationFactory.Create<MongoSettings>();
            var proxySettings = configurationFactory.Create<ProxySettings>();
            var redisSettings = Configuration.GetSection("RedisSettings").Get<RedisSettings>() ?? new RedisSettings();


            // Register GraphQL types via DI and rely on IServiceProvider-based schema
            services.AddMvc();

            // Configure Redis distributed cache if enabled
            if (redisSettings.Enabled)
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisSettings.ConnectionString;
                    options.InstanceName = redisSettings.InstanceName;
                });
            }

            services.AddHealthChecks()
                .AddCheck<MongoHealthCheck>("MongoDB Database")
                .AddCheck<StaticProxyHealthCheck>("Proxy Health")
                .AddCheck<RedisHealthCheck>("Redis Cache");
            services.AddSingleton(status);
            services.AddSingleton(settings);
            services.AddSingleton(proxySettings);
            services.AddSingleton(redisSettings);
            services.AddSingleton(configurationFactory);

            services.AddSingleton<SlipkaContext>();
            services.AddTransient<ISessionRepository, SessionRepository>();
            services.AddTransient<IMessageRepository, MessageRepository>();
            services.AddTransient<IFileRepository, FileRepository>();
            services.AddSingleton<ProxyStore>();
            services.AddSingleton<GridFsCleanupLoop>();
            services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();

            // Static proxy configuration and validation
            var staticProxySettings = Configuration.GetSection("StaticProxies").Get<Configuration.StaticProxySettings>() ?? new Configuration.StaticProxySettings();
            var validator = new Configuration.StaticProxyValidator(proxySettings);
            var validationResult = validator.Validate(staticProxySettings);

            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                throw new InvalidOperationException($"Static proxy configuration validation failed: {errors}");
            }

            if (validationResult.Warnings.Any())
            {
                var warnings = string.Join("; ", validationResult.Warnings);
                Console.WriteLine($"Static proxy configuration warnings: {warnings}");
            }

            services.AddSingleton(staticProxySettings);
            services.AddSingleton(validator);
            services.AddSingleton<StaticProxyManager>();
            services.AddHostedService<StaticProxyInitializationService>();

            // Preprocessor configuration and services
            var preprocessorSettings = Configuration.GetSection("PreprocessorSettings").Get<PreprocessorSettings>() ?? new PreprocessorSettings();
            services.AddSingleton(preprocessorSettings);

            // Register preprocessor registry and factory
            services.AddSingleton<Slipka.Preprocessors.Interfaces.IPreprocessorRegistry, Slipka.Preprocessors.PreprocessorRegistry>();
            services.AddSingleton<Slipka.Preprocessors.Interfaces.IPreprocessorFactory, Slipka.Preprocessors.PreprocessorFactory>();

            // Register preprocessor initialization service
            services.AddHostedService<PreprocessorInitializationService>();


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = WriteHealthCheckResponse
                });
            });
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        private static Task WriteHealthCheckResponse(HttpContext context, HealthReport healthReport)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                status = healthReport.Status.ToString(),
                totalDuration = healthReport.TotalDuration,
                dependencies = healthReport.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    duration = entry.Value.Duration,
                    description = entry.Value.Description,
                    data = entry.Value.Data,
                    exception = entry.Value.Exception?.Message
                }),
                unhealthyReasons = healthReport.Entries
                    .Where(entry => entry.Value.Status != HealthStatus.Healthy)
                    .Select(entry => new
                    {
                        dependency = entry.Key,
                        status = entry.Value.Status.ToString(),
                        reason = entry.Value.Description ?? "No description provided",
                        exception = entry.Value.Exception?.Message,
                        duration = entry.Value.Duration
                    })
                    .ToList()
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(response, jsonOptions);
            return context.Response.WriteAsync(json);
        }


    }
}
