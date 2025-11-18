using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Slipka.ApiArguments
{
    /// <summary>
    /// Example usage of LogMessage API
    /// POST /api/logging
    /// Content-Type: application/json
    ///
    /// {
    ///     "correlationId": "550e8400-e29b-41d4-a716-446655440000",
    ///     "level": "Information",
    ///     "message": "User login successful",
    ///     "properties": {
    ///         "userId": "12345",
    ///         "ipAddress": "192.168.1.100",
    ///         "userAgent": "Mozilla/5.0..."
    ///     },
    ///     "category": "Authentication",
    ///     "timestamp": "2023-11-18T10:30:00Z"
    /// }
    ///
    /// Valid log levels: Trace, Debug, Information, Warning, Error, Critical
    /// </summary>
    public class LogMessageExample
    {
        public static LogMessage CreateExample()
        {
            return new LogMessage
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Level = "Information",
                Message = "Service operation completed successfully",
                Properties = JObject.FromObject(new
                {
                    operation = "user_registration",
                    duration_ms = 150,
                    success = true
                }),
                Category = "BusinessLogic",
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
