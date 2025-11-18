using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> _logger)
    {
        this._logger = _logger;
    }

    [HttpGet("echo")]
    public IActionResult Echo([FromQuery] string message = "Hello World")
    {
        _logger.LogInformation("Echo endpoint called with message: {Message}", message);
        return Ok(new { message, timestamp = DateTime.UtcNow, headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()) });
    }

    [HttpGet("status/{statusCode}")]
    public IActionResult Status(int statusCode)
    {
        _logger.LogInformation("Status endpoint called with code: {StatusCode}", statusCode);
        return StatusCode(statusCode, new { statusCode, message = $"Returned status {statusCode}" });
    }

    [HttpPost("echo")]
    public IActionResult EchoPost([FromBody] EchoRequest request)
    {
        _logger.LogInformation("Echo POST endpoint called with: {@Request}", request);
        return Ok(new
        {
            received = request,
            timestamp = DateTime.UtcNow,
            headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        });
    }

    [HttpGet("slow/{delayMs}")]
    public async Task<IActionResult> Slow(int delayMs)
    {
        _logger.LogInformation("Slow endpoint called with delay: {DelayMs}ms", delayMs);
        await Task.Delay(delayMs);
        return Ok(new { delayMs, completedAt = DateTime.UtcNow });
    }

    [HttpGet("headers")]
    public IActionResult Headers()
    {
        _logger.LogInformation("Headers endpoint called");
        return Ok(Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
    }

    [HttpGet("user/{userId}")]
    public IActionResult GetUser(string userId)
    {
        _logger.LogInformation("Get user endpoint called for: {UserId}", userId);

        if (userId == "notfound")
        {
            return NotFound(new { error = "User not found", userId });
        }

        return Ok(new
        {
            userId,
            name = $"User {userId}",
            email = $"{userId}@example.com",
            createdAt = DateTime.UtcNow.AddDays(-30)
        });
    }

    [HttpPost("data")]
    public IActionResult ProcessData([FromBody] DataRequest request)
    {
        _logger.LogInformation("Process data endpoint called with: {@Request}", request);

        if (request.Validate)
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { error = "Name is required" });
            }

            if (request.Value < 0)
            {
                return BadRequest(new { error = "Value must be non-negative" });
            }
        }

        return Ok(new
        {
            processed = true,
            request,
            processedAt = DateTime.UtcNow,
            result = request.Value * 2
        });
    }

    [HttpGet("large")]
    public IActionResult LargeResponse()
    {
        _logger.LogInformation("Large response endpoint called");
        var largeData = new
        {
            data = Enumerable.Range(1, 1000).Select(i => new
            {
                id = i,
                name = $"Item {i}",
                description = $"This is a description for item {i} with some additional text to make it larger",
                value = i * 10,
                createdAt = DateTime.UtcNow.AddDays(-i)
            }).ToArray()
        };

        return Ok(largeData);
    }

    [HttpGet("auth-required")]
    public IActionResult AuthRequired()
    {
        _logger.LogInformation("Auth required endpoint called");

        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Unauthorized(new { error = "Authorization header required" });
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new { error = "Bearer token required" });
        }

        return Ok(new
        {
            authenticated = true,
            token = authHeader.Substring(7),
            timestamp = DateTime.UtcNow
        });
    }
}

public class EchoRequest
{
    public string Message { get; set; } = string.Empty;
    public int Number { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class DataRequest
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool Validate { get; set; } = true;
    public List<string> Tags { get; set; } = new();
}
