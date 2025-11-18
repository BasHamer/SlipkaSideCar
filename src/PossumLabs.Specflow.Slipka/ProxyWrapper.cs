using PossumLabs.Specflow.Core;
using PossumLabs.Specflow.Core.Variables;
using PossumLabs.Specflow.Slipka.ValueObjects;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PossumLabs.Specflow.Slipka
{
    public class ProxyWrapper : IEntity
    {
        public ProxyWrapper(Uri host, Uri destination, TimeSpan? openFor = null, TimeSpan? retainedFor =  null) : this(host)
        {
            Open(destination, openFor, retainedFor);
        }

        public ProxyWrapper(Uri host)
        {
            AdministrationUri = host;
            AdministrationClient = new RestClient(host);
        }

        private Uri AdministrationUri { get; }
        private RestClient AdministrationClient { get; }
        private RestClient ProxyClient { get; set; }
        private SessionSummary ProxySession { get; set; }

        public Uri ProxyUri { get; private set; }
        public string Id { get => ProxySession.Id; }

        public void Open(Uri destination, TimeSpan? openFor = null, TimeSpan? retainedFor = null)
        {
            ProxySession = new SessionSummary
            {
                TargetHost = destination.Host,
                TargetPort = destination.Port,
                OpenFor = openFor.HasValue ? openFor.ToString() : null,
                RetainedFor = retainedFor.HasValue ? retainedFor.ToString() : null
            };

            var request = new RestRequest("/api/proxies", Method.Post)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(ProxySession);
            var response = AdministrationClient.Execute<SessionSummary>(request);
            if (!response.IsSuccessful)
                throw new Exception($"Was unable to open the proxy, error was {response.StatusCode} {response.StatusDescription}");
            ProxySession = response.Data;
            ProxyUri = new Uri($"http://{AdministrationUri.Host}:{ProxySession.ProxyPort}");
            ProxyClient = new RestClient(ProxyUri);
        }

        public void LogsResponsesOfType(string type, string value)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/record", Method.Put);
            var call = new Call
            {
                Response = new Message
                {
                    Headers = new List<Header>()
                }
            };
            call.Response.Headers.Add(new Header(type, new List<string>() { value }));
            request.RequestFormat = DataFormat.Json;
            request.AddBody(call);
            AdministrationClient.Execute(request);
        }

        private void Execute(RestRequest request)
        {
            var response = AdministrationClient.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new InvalidOperationException(response.StatusCode.ToString());
        }

        public void LogsCallsTo(Uri uri)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/record", Method.Put);
            var call = new Call
            {
                Uri = uri
            };
            request.RequestFormat = DataFormat.Json;
            request.AddBody(call);
            Execute(request);
        }

        public void RegisterTag(CallTemplate call)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/tag", Method.Put)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(call);
            Execute(request);
        }

        public void RegisterRecording(CallTemplate call)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/record", Method.Put)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(call);
            Execute(request);
        }

        public void RegisterInject(CallTemplate call)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/inject", Method.Put)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(call);
            Execute(request);
        }

        public void RegisterDecoration(Header header)
        {
            var request = new RestRequest($"/api/proxies/{ProxySession.Id}/decorate", Method.Put)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(header);
            Execute(request);
        }

        public Session GetSession()
        {
            var request = new RestRequest($"/api/SessionsApi/{ProxySession.Id}", Method.Get);
            var response = AdministrationClient.Execute<Session>(request);
            return response.Data;
        }

        public CallCollection GetCalls(bool? recorded = null, string tag = null)
        {
            var request = new RestRequest($"/api/SessionsApi/{ProxySession.Id}/calls", Method.Get);

            if (recorded.HasValue)
                request.AddQueryParameter("recorded", recorded.Value.ToString().ToLower());
            if (tag != null)
                request.AddQueryParameter("tag", tag);

            var response = AdministrationClient.Execute<CallRecord[]>(request);
            return new CallCollection(response.Data ?? new CallRecord[0]);
        }

        public void CloseAsync()
        {
            if (ProxySession == null)
                return;
            AdministrationClient.ExecuteAsync(new RestRequest(
                $"/api/proxies/{ProxySession.Id}",
                Method.Delete));
        }

        public void Close()
        {
            if (ProxySession == null)
                return;
            AdministrationClient.Execute(new RestRequest(
                $"/api/proxies/{ProxySession.Id}",
                Method.Delete));
        }

        public RestResponse Call(string path, Method method)
        {
            var request = new RestRequest(path, method)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(ProxySession);
            return AdministrationClient.Execute(request);
        }

        public RestResponse<T> Call<T>(string path, Method method) where T : new()
        {
            var request = new RestRequest(path, method)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddBody(ProxySession);
            return AdministrationClient.Execute<T>(request);
        }

        public byte[] DownloadRequest(int number)
            => AdministrationClient.DownloadData(new RestRequest($"/api/sessions/{ProxySession.Id}/request/{number}"));

        public byte[] DownloadResponse(int number)
            => AdministrationClient.DownloadData(new RestRequest($"/api/sessions/{ProxySession.Id}/response/{number}"));

        public string LogFormat()
            => ProxySession.Id;
    }
}
