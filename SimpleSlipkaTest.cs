using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class SimpleSlipkaTest
{
    public static async Task Main()
    {
        Console.WriteLine("🧪 Running simplest Slipka integration test...");
        Console.WriteLine("This test will verify that:");
        Console.WriteLine("1. Slipka health endpoint responds");
        Console.WriteLine("2. Proxy creation API works");
        Console.WriteLine("3. Basic proxy properties are correct");
        Console.WriteLine("");

        try
        {
            using var httpClient = new HttpClient();

            // Test 1: Check Slipka health
            Console.WriteLine("1. Testing Slipka health endpoint...");
            var healthResponse = await httpClient.GetAsync("http://localhost:4445/health");

            if (!healthResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ FAILED: Slipka health check failed. Status: {healthResponse.StatusCode}");
                return;
            }

            var healthContent = await healthResponse.Content.ReadAsStringAsync();
            if (!healthContent.Contains("Healthy"))
            {
                Console.WriteLine($"❌ FAILED: Slipka returned '{healthContent}' instead of 'Healthy'");
                return;
            }

            Console.WriteLine("✅ Slipka is healthy");

            // Test 2: Create a proxy
            Console.WriteLine("2. Testing proxy creation...");
            var proxyData = new
            {
                targetHost = "httpbin.org",
                targetPort = 80
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(proxyData),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var createResponse = await httpClient.PostAsync("http://localhost:4445/api/Proxies", jsonContent);

            if (!createResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ FAILED: Proxy creation failed. Status: {createResponse.StatusCode}");
                var errorContent = await createResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {errorContent}");
                return;
            }

            var createContent = await createResponse.Content.ReadAsStringAsync();
            var proxy = JsonSerializer.Deserialize<ProxyResponse>(createContent);

            if (proxy == null || string.IsNullOrEmpty(proxy.Id))
            {
                Console.WriteLine($"❌ FAILED: Invalid proxy response: {createContent}");
                return;
            }

            Console.WriteLine($"✅ Proxy created successfully: ID={proxy.Id}, Port={proxy.ProxyPort}");

            // Test 3: Basic validation
            if (proxy.ProxyPort < 61710 || proxy.ProxyPort > 61920)
            {
                Console.WriteLine($"❌ FAILED: Proxy port {proxy.ProxyPort} is outside expected range [61710-61920]");
                return;
            }

            if (proxy.TargetHost != "httpbin.org" || proxy.TargetPort != 80)
            {
                Console.WriteLine($"❌ FAILED: Proxy target mismatch. Expected httpbin.org:80, got {proxy.TargetHost}:{proxy.TargetPort}");
                return;
            }

            if (proxy.LeaveProxyOpenUntil <= DateTime.UtcNow)
            {
                Console.WriteLine($"❌ FAILED: LeaveProxyOpenUntil is in the past: {proxy.LeaveProxyOpenUntil}");
                return;
            }

            if (proxy.RetainDataUntil <= DateTime.UtcNow)
            {
                Console.WriteLine($"❌ FAILED: RetainDataUntil is in the past: {proxy.RetainDataUntil}");
                return;
            }

            Console.WriteLine("✅ All proxy properties are correct");

            Console.WriteLine("");
            Console.WriteLine("🎉 SUCCESS: The simplest integration test is now passing!");
            Console.WriteLine("");
            Console.WriteLine("Summary of what worked:");
            Console.WriteLine("- ✅ Slipka health endpoint responds correctly");
            Console.WriteLine("- ✅ Proxy creation API accepts requests and returns valid JSON");
            Console.WriteLine("- ✅ Proxy configuration includes all required fields");
            Console.WriteLine("- ✅ Port assignment is in the correct range (61710-61920)");
            Console.WriteLine("- ✅ Target host and port are preserved correctly");
            Console.WriteLine("- ✅ Timestamps for proxy lifetime are set in the future");
            Console.WriteLine("");
            Console.WriteLine("This proves the core Slipka functionality is working!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR: Test failed with exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}

public class ProxyResponse
{
    public string Id { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public string TargetHost { get; set; } = string.Empty;
    public int TargetPort { get; set; }
    public DateTime LeaveProxyOpenUntil { get; set; }
    public DateTime RetainDataUntil { get; set; }
}
