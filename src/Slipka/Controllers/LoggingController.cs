using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Slipka.ApiArguments;

namespace Slipka.Controllers
{
    [Route("api/logging")]
    [ApiController]
    public class LoggingController : ControllerBase
    {
        private readonly ILogger<LoggingController> _logger;

        public LoggingController(ILogger<LoggingController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult LogMessage([FromBody] LogMessage logMessage)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Validate correlation ID format (basic UUID or GUID check)
                if (!IsValidCorrelationId(logMessage.CorrelationId))
                {
                    return BadRequest("Invalid correlation ID format");
                }

                // Set timestamp if not provided
                var timestamp = logMessage.Timestamp ?? DateTime.UtcNow;

                // Parse log level
                if (!Enum.TryParse(logMessage.Level, true, out LogLevel logLevel))
                {
                    logLevel = LogLevel.Information; // Default to Information if parsing fails
                }

                // Use Serilog context to add correlation ID and other properties
                using (LogContext.PushProperty("CorrelationId", logMessage.CorrelationId))
                using (LogContext.PushProperty("OriginalCorrelationSubId", logMessage.OriginalCorrelationSubId))
                using (LogContext.PushProperty("NewCorrelationSubId", logMessage.NewCorrelationSubId))
                using (LogContext.PushProperty("Category", logMessage.Category ?? "Application"))
                {
                    // Add custom properties if provided
                    if (logMessage.Properties != null)
                    {
                        foreach (var property in logMessage.Properties)
                        {
                            LogContext.PushProperty(property.Key, property.Value);
                        }
                    }

                    // Log the message using Microsoft.Extensions.Logging
                    // This will be picked up by Serilog through the configured bridge
                    var loggerMessage = $"{logMessage.Message}";
                    if (!string.IsNullOrEmpty(logMessage.Exception))
                    {
                        loggerMessage += $" Exception: {logMessage.Exception}";
                    }

                    _logger.Log(logLevel, loggerMessage);
                }

                return Ok(new { message = "Log entry recorded", correlationId = logMessage.CorrelationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing log message with correlation ID {CorrelationId}", logMessage.CorrelationId);
                return StatusCode(500, "Internal server error while processing log message");
            }
        }

        private bool IsValidCorrelationId(string correlationId)
        {
            // Basic validation - should be a non-empty string, typically GUID format
            return !string.IsNullOrWhiteSpace(correlationId) &&
                   correlationId.Length >= 1 &&
                   correlationId.Length <= 100 &&
                   !correlationId.Contains("<script") && // Basic XSS protection
                   !correlationId.Contains("javascript:"); // Basic XSS protection
        }
    }
}
