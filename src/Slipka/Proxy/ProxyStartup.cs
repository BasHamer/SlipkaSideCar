using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Slipka.DomainObjects;
using Slipka.Repositories;
using Serilog;

namespace Slipka.Proxy
{
    public class ProxyStartup
    {
        public ProxyStartup(
            IConfiguration configuration,
            Session session,
            IFileRepository fileRepository,
            IMessageRepository messageRepository,
            EventHandler<SessionEventArgs> sessionUpdate,
            ILogger<ProxyStartup> logger
            )
        {
            Configuration = configuration;
            Session = session;
            Target = new HostString(Session.TargetHost, Session.TargetPort.Value);
            FileRepository = fileRepository;
            MessageRepository = messageRepository;
            SessionUpdate = sessionUpdate;
            Logger = logger;
        }

        private EventHandler<SessionEventArgs> SessionUpdate { get;}
        private IFileRepository FileRepository { get; }
        private IMessageRepository MessageRepository { get; }
        public IConfiguration Configuration { get; }
        public Session Session { get; }
        public HostString Target { get; }
        private ILogger<ProxyStartup> Logger { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
            var proxyHandlerLogger = loggerFactory.CreateLogger<ProxyHandler>();
            var handler = new ProxyHandler(Session, FileRepository, MessageRepository, proxyHandlerLogger);
            handler.ImportantDataAddedEvent += SessionUpdate;
            services.AddMvc();
            services.AddProxy(options =>
           {
               options.MessageHandler = handler;
               options.PrepareRequest = (originalRequest, message) =>
               {
                   message.Headers.Add("X-Forwarded-Host", originalRequest.Host.Host);
                   return Task.FromResult(0);
               };
           });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.MapWhen(context => true, builder => builder.RunProxy(new ProxyOptions
            {
                Scheme = Session.TargetPortHttps ? "https" : "http",
                Host = Target
            }));
        }
    }
}
