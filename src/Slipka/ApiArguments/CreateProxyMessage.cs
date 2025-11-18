using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Slipka.ApiArguments
{
    public class CreateProxyMessage
    {
        public CreateProxyMessage()
        {
            Tags = new List<string>();
            RecordedCalls = new List<RecordMessage>();
            InjectedCalls = new List<InjectMessage>();
            TaggedCalls = new List<TagMessage>();
            Decorations = new List<DecorateMessage>();
            Preprocessors = new List<PreprocessorMessage>();
        }

        public string Name { get; set; }

        [Required]
        public string TargetHost { get; set; }
        public int? TargetPort { get; set; }
        public bool ProxyPortHttps { get; set; }
        public bool TargetPortHttps { get; set; }

        public List<string> Tags { get; set; }

        public List<RecordMessage> RecordedCalls { get; set; }

        public List<InjectMessage> InjectedCalls { get; set; }

        public List<TagMessage> TaggedCalls { get; set; }

        public List<DecorateMessage> Decorations { get; set; }

        public List<PreprocessorMessage> Preprocessors { get; set; }

        [IsFuture()]
        public string RetainedFor { get; set; }

        [IsFuture()]
        public string OpenFor { get; set; }

        public int? MaxCallsInMemory { get; set; } // Maximum number of recent calls to keep in memory (null = unlimited)
    }
}
