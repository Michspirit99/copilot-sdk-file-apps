#!/usr/bin/env dotnet
#:package GitHub.Copilot.SDK@0.1.23
#:package Microsoft.Extensions.AI@10.2.0
#:package System.Net.Http.Json@10.0.2

using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;

// AI-Powered API Test Generator
// Generates comprehensive test cases for REST APIs from OpenAPI specs or API exploration

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run api-test-generator.cs -- <swagger-url-or-file> [format]");
    Console.WriteLine("\nFormats:");
    Console.WriteLine("  xunit    - Generate xUnit test cases (default)");
    Console.WriteLine("  postman  - Generate Postman collection");
    Console.WriteLine("  curl     - Generate curl commands");
    Console.WriteLine("\nExamples:");
    Console.WriteLine("  dotnet run api-test-generator.cs -- https://api.example.com/swagger.json");
    Console.WriteLine("  dotnet run api-test-generator.cs -- swagger.yaml xunit");
    Console.WriteLine("  dotnet run api-test-generator.cs -- openapi.json postman");
    return 1;
}

string specSource = args[0];
string format = args.Length > 1 ? args[1].ToLower() : "xunit";

Console.WriteLine($"🧪 API Test Generator");
Console.WriteLine($"📋 Spec: {specSource}");
Console.WriteLine($"📝 Format: {format}\n");

// Load API spec
string apiSpec = "";
bool isUrl = specSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
             specSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

try
{
    if (isUrl)
    {
        Console.WriteLine("⬇️  Downloading spec...");
        using var httpClient = new HttpClient();
        apiSpec = await httpClient.GetStringAsync(specSource);
    }
    else
    {
        if (!File.Exists(specSource))
        {
            Console.WriteLine($"❌ Error: File not found: {specSource}");
            return 1;
        }
        apiSpec = await File.ReadAllTextAsync(specSource);
    }
    
    Console.WriteLine($"✓ Loaded spec ({apiSpec.Length:N0} characters)\n");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error loading spec: {ex.Message}");
    return 1;
}

// Define tools for API analysis
var parseEndpointsTool = AIFunctionFactory.Create(
    ([Description("OpenAPI/Swagger JSON or YAML content")] string spec) =>
    {
        try
        {
            // Try to parse as JSON
            var doc = JsonDocument.Parse(spec);
            var endpoints = new List<(string Path, string Method, string Summary, string OperationId)>();
            
            if (doc.RootElement.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    foreach (var method in path.Value.EnumerateObject())
                    {
                        endpoints.Add(
                            (
                                path.Name,
                                method.Name.ToUpperInvariant(),
                                method.Value.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "",
                                method.Value.TryGetProperty("operationId", out var o) ? (o.GetString() ?? "") : ""
                            )
                        );
                        if (endpoints.Count > 50) break; // Limit
                    }
                    if (endpoints.Count > 50) break;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"success: true");
            sb.AppendLine($"count: {endpoints.Count}");
            sb.AppendLine("endpoints:");
            foreach (var ep in endpoints)
            {
                var summary = string.IsNullOrWhiteSpace(ep.Summary) ? "" : $" — {ep.Summary}";
                var opId = string.IsNullOrWhiteSpace(ep.OperationId) ? "" : $" (operationId: {ep.OperationId})";
                sb.AppendLine($"- {ep.Method} {ep.Path}{summary}{opId}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"success: false\nmessage: {ex.Message}\n";
        }
    },
    "parse_endpoints",
    "Parse endpoints from OpenAPI/Swagger specification"
);

var analyzeAuthTool = AIFunctionFactory.Create(
    ([Description("OpenAPI spec content")] string spec) =>
    {
        var authTypes = new List<string>();
        
        if (spec.Contains("\"security\"") || spec.Contains("securitySchemes"))
        {
            if (spec.Contains("bearer", StringComparison.OrdinalIgnoreCase))
                authTypes.Add("Bearer Token");
            if (spec.Contains("apiKey", StringComparison.OrdinalIgnoreCase))
                authTypes.Add("API Key");
            if (spec.Contains("oauth", StringComparison.OrdinalIgnoreCase))
                authTypes.Add("OAuth2");
            if (spec.Contains("basic", StringComparison.OrdinalIgnoreCase))
                authTypes.Add("Basic Auth");
        }

        if (authTypes.Count == 0)
            return "authenticationTypes: (none detected)\n";

        return "authenticationTypes:\n" + string.Join("\n", authTypes.Select(a => $"- {a}")) + "\n";
    },
    "analyze_auth",
    "Analyze authentication requirements"
);

var generateTestCasesTool = AIFunctionFactory.Create(
    (
        [Description("HTTP method (GET, POST, etc.)")] string method,
        [Description("API endpoint path")] string path,
        [Description("Test format (xunit, postman, curl)")] string testFormat) =>
    {
        var testCases = new List<string>
        {
            $"Test {method} {path} - Success (200)",
            $"Test {method} {path} - Not Found (404)",
            $"Test {method} {path} - Unauthorized (401)",
            $"Test {method} {path} - Invalid Input (400)"
        };
        
        if (method == "POST" || method == "PUT" || method == "PATCH")
        {
            testCases.Add($"Test {method} {path} - Missing Required Fields");
            testCases.Add($"Test {method} {path} - Invalid Data Types");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"endpoint: {method} {path}");
        sb.AppendLine("testCases:");
        foreach (var tc in testCases)
            sb.AppendLine($"- {tc}");
        return sb.ToString();
    },
    "generate_test_cases",
    "Generate test case scenarios for an endpoint"
);

// Create Copilot session
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true,
    Tools = new[] { parseEndpointsTool, analyzeAuthTool, generateTestCasesTool }
});

var generationComplete = new TaskCompletionSource();
var outputBuilder = new System.Text.StringBuilder();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            outputBuilder.Append(delta.Data.DeltaContent);
            break;
            
        case ToolExecutionStartEvent toolStart:
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n🔧 {toolStart.Data.ToolName}...");
            Console.ResetColor();
            break;
            
        case SessionIdleEvent:
            Console.WriteLine("\n");
            generationComplete.SetResult();
            break;
    }
});

// Generate prompt based on format
var prompt = format switch
{
    "postman" => $@"Generate a Postman collection for this API. 
1. Parse all endpoints from the spec using parse_endpoints
2. Analyze authentication using analyze_auth
3. For each endpoint, create Postman requests with:
   - Variables for base URL and auth tokens
   - Request examples with sample payloads
   - Tests for status codes and response validation
4. Generate the complete Postman collection JSON

OpenAPI Spec:
{apiSpec}",

    "curl" => $@"Generate curl command examples for testing this API.
1. Parse all endpoints
2. Analyze authentication requirements
3. For each endpoint, generate:
   - Basic curl command with proper method
   - Example with authentication headers
   - Example with request body (for POST/PUT)
   - Common error case examples

OpenAPI Spec:
{apiSpec}",

    _ => $@"Generate xUnit test cases for this API.
1. Parse all endpoints using parse_endpoints
2. Analyze authentication using analyze_auth
3. For each major endpoint, use generate_test_cases to create test scenarios
4. Generate C# xUnit test class code with:
   - Setup method with HttpClient and authentication
   - Test methods for happy path and error cases
   - Assertions for status codes and response validation
   - Proper test naming conventions

Format the output as complete, runnable C# test code.

OpenAPI Spec:
{apiSpec}"
};

await session.SendAsync(new MessageOptions { Prompt = prompt });
await generationComplete.Task;

// Save output to file
var outputFileName = format switch
{
    "postman" => "api-tests.postman_collection.json",
    "curl" => "api-tests.sh",
    _ => "ApiTests.cs"
};

var outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputFileName);
await File.WriteAllTextAsync(outputPath, outputBuilder.ToString());

Console.WriteLine($"✅ Tests generated!");
Console.WriteLine($"💾 Saved to: {outputPath}");
Console.WriteLine($"💡 Tip: Review and customize the generated tests before use");

return 0;
