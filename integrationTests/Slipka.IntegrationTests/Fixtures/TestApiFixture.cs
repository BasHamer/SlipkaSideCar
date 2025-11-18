using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using Xunit;
using System.Threading.Tasks;

namespace Slipka.IntegrationTests.Fixtures;

public class TestApiFixture : IAsyncLifetime
{
    private Process? _testApiProcess;
    private readonly TestConfiguration _config;

    public TestApiFixture()
    {
        _config = new TestConfiguration();
    }

    public async Task InitializeAsync()
    {
        await StartTestApiAsync();
        await WaitForTestApiReadyAsync();
    }

    public async Task DisposeAsync()
    {
        await StopTestApiAsync();
    }

    private async Task StartTestApiAsync()
    {
        // Navigate from bin/Debug/net8.0 up to the project root, then to TestApi
        var currentDir = Directory.GetCurrentDirectory();
        var binDir = Directory.GetParent(currentDir)!;
        var debugDir = Directory.GetParent(binDir.FullName)!;
        var projectDir = Directory.GetParent(debugDir.FullName)!;
        var testApiPath = Path.Combine(projectDir.FullName, "TestApi");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run",
            WorkingDirectory = testApiPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _testApiProcess = Process.Start(startInfo);

        if (_testApiProcess == null)
        {
            throw new InvalidOperationException("Failed to start TestApi process");
        }

        // Give the process a moment to start
        await Task.Delay(1000);
    }

    private async Task StopTestApiAsync()
    {
        if (_testApiProcess != null && !_testApiProcess.HasExited)
        {
            _testApiProcess.Kill(true);
            await _testApiProcess.WaitForExitAsync();
        }
    }

    private async Task WaitForTestApiReadyAsync(int timeoutSeconds = 30)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var timeout = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var response = await httpClient.GetAsync(_config.TestApiBaseUrl + "/api/test/echo?message=healthcheck");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Test API not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"TestApi did not become ready within {timeoutSeconds} seconds");
    }
}
