using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Slipka.ApiArguments
{
    public class LogMessage
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string CorrelationId { get; set; }

        public string OriginalCorrelationSubId { get; set; }

        public string NewCorrelationSubId { get; set; }

        [Required]
        public string Level { get; set; }

        [Required]
        public string Message { get; set; }

        public JObject Properties { get; set; }

        public string Category { get; set; }

        public DateTime? Timestamp { get; set; }

        public string Exception { get; set; }
    }
}
