using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Slipka.Preprocessors.Interfaces;
using Slipka.ValueObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.DomainObjects
{
    public class Session
    {
        public Session()
        {
            Id = Guid.NewGuid().ToString();
            Calls = new ConcurrentQueue<Call>();
            Tags = new ConcurrentBag<string>();
            RecordedCalls = new ConcurrentBag<CallTemplate>();
            InjectedCalls = new ConcurrentBag<CallTemplate>();
            TaggedCalls = new ConcurrentBag<CallTemplate>();
            Decorations = new ConcurrentBag<Header>();
            Preprocessors = new ConcurrentBag<IPreprocessor>();
        }
        [JsonIgnore]
        [BsonId]
        public ObjectId InternalId { get; set; }
        [BsonElement("public_id")]
        public string Id { get; set; }
        [BsonElement("name")]
        public string Name { get; set; }
        [BsonElement("calls")]
        public ConcurrentQueue<Call> Calls { get; set; }
        [BsonElement("proxy_port")]
        public int ProxyPort { get; set; }
        [Required]
        [BsonElement("target_host")]
        public string TargetHost { get; set; }
        [BsonElement("target_port")]
        public int? TargetPort { get; set; }
        [BsonElement("proxy_port_https")]
        public bool ProxyPortHttps { get; set; }
        [BsonElement("target_port_https")]
        public bool TargetPortHttps { get; set; }

        [BsonElement]
        public ConcurrentBag<string> Tags { get; set; }

        [BsonElement("recorded_calls")]
        public ConcurrentBag<CallTemplate> RecordedCalls { get; set; }
        [BsonElement("overridden_calls")]
        public ConcurrentBag<CallTemplate> InjectedCalls { get; set; }
        [BsonElement("tagged_calls")]
        public ConcurrentBag<CallTemplate> TaggedCalls { get; set; }
        [BsonElement("decorations")]
        public ConcurrentBag<Header> Decorations { get; set; }
        [BsonIgnore] // Preprocessors are not serialized to MongoDB as they contain runtime state
        public ConcurrentBag<IPreprocessor> Preprocessors { get; set; }

        [BsonElement("retain_data_until")]
        public DateTime RetainDataUntil { get; set; }

        [BsonElement("max_calls_in_memory")]
        public int? MaxCallsInMemory { get; set; }

        [BsonIgnore]
        public DateTime LeaveProxyOpenUntil { get; set; }

        [BsonIgnore]
        public bool Active { get; set; }

        public int State()
        {
            int state = 0;
            state += Calls.Count(x => x.RequestId != ObjectId.Empty);
            state += Calls.Count(x => x.ResponseId != ObjectId.Empty);
            state += Tags.Count;
            return state;
        }
    }
}
