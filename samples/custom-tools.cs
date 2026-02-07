// =============================================================================
// custom-tools.cs — Define custom tools the Copilot agent can invoke
// Run: dotnet run samples/custom-tools.cs
// =============================================================================

#:package GitHub.Copilot.SDK@*-*
#:package Microsoft.Extensions.AI@*-*

using System.ComponentModel;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

Console.WriteLine("🔧 Custom Tools — Extend the AI with your own functions");
Console.WriteLine("========================================================");
Console.WriteLine();

await using var client = new CopilotClient();
await client.StartAsync();

// Define custom tools using Microsoft.Extensions.AI's AIFunctionFactory
var tools = new[]
{
    AIFunctionFactory.Create(
        ([Description("The city name to get weather for")] string city) =>
        {
            // Simulated weather data — in production, call a real API
            var weatherData = new Dictionary<string, (int Temp, string Condition)>
            {
                ["Seattle"] = (52, "Rainy"),
                ["San Francisco"] = (65, "Foggy"),
                ["New York"] = (45, "Cloudy"),
                ["Austin"] = (78, "Sunny"),
                ["London"] = (48, "Overcast"),
            };

            if (weatherData.TryGetValue(city, out var data))
            {
                return $"Weather in {city}: {data.Temp}°F, {data.Condition}";
            }
            return $"Weather in {city}: 70°F, Clear (default data)";
        },
        "get_weather",
        "Get current weather for a city"),

    AIFunctionFactory.Create(
        ([Description("Mathematical expression to evaluate")] string expression) =>
        {
            // Simple expression evaluator for demo purposes
            try
            {
                var result = expression switch
                {
                    var e when e.Contains('+') => EvalSimple(e, '+'),
                    var e when e.Contains('-') => EvalSimple(e, '-'),
                    var e when e.Contains('*') => EvalSimple(e, '*'),
                    var e when e.Contains('/') => EvalSimple(e, '/'),
                    _ => $"Cannot evaluate: {expression}"
                };
                return result;
            }
            catch
            {
                return $"Error evaluating: {expression}";
            }
        },
        "calculate",
        "Evaluate a simple mathematical expression"),

    AIFunctionFactory.Create(
        ([Description("Number of items to list")] int count) =>
        {
            var facts = new[]
            {
                "C# was first released in 2000.",
                ".NET 10 is the first LTS release to support file-based apps.",
                "The Copilot SDK communicates with the CLI via JSON-RPC.",
                "File-based apps support the #:package directive for NuGet packages.",
                "C# 14 introduces field-backed properties.",
                "The dotnet CLI now supports native tab-completion scripts.",
                ".NET 10 includes post-quantum cryptography support.",
                "Aspire 13 ships alongside .NET 10.",
            };

            var selected = facts.Take(Math.Min(count, facts.Length));
            return string.Join("\n", selected.Select((f, i) => $"{i + 1}. {f}"));
        },
        "get_dotnet_facts",
        "Get interesting facts about .NET and C#")
};

// Create a session with our custom tools
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Tools = tools,
    Streaming = true
});

Console.WriteLine("✅ Session created with 3 custom tools:");
Console.WriteLine("   🌤️  get_weather — Get weather for a city");
Console.WriteLine("   🔢 calculate — Evaluate math expressions");
Console.WriteLine("   📝 get_dotnet_facts — Get .NET trivia");
Console.WriteLine();

var done = new TaskCompletionSource();

session.On(evt =>
{
    switch (evt)
    {
        case ToolExecutionStartEvent toolStart:
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"   🔧 Calling tool: {toolStart.Data.ToolName}");
            Console.ResetColor();
            break;
        case ToolExecutionCompleteEvent toolDone:
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"   ✅ Tool completed");
            Console.ResetColor();
            break;
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            break;
        case AssistantMessageEvent:
            Console.WriteLine();
            break;
        case SessionIdleEvent:
            done.SetResult();
            break;
        case SessionErrorEvent err:
            Console.WriteLine($"\n❌ Error: {err.Data.Message}");
            done.SetResult();
            break;
    }
});

// Ask something that requires tool usage
var prompt = "What's the weather in Seattle and Austin? Also calculate 42 * 17, and give me 3 .NET facts.";
Console.WriteLine($"📤 Prompt: {prompt}");
Console.WriteLine();

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine("✅ Done! The AI used custom tools defined right here in this file.");

// Helper for simple math
static string EvalSimple(string expr, char op)
{
    var parts = expr.Split(op).Select(p => double.Parse(p.Trim())).ToArray();
    var result = op switch
    {
        '+' => parts[0] + parts[1],
        '-' => parts[0] - parts[1],
        '*' => parts[0] * parts[1],
        '/' => parts[1] != 0 ? parts[0] / parts[1] : double.NaN,
        _ => double.NaN
    };
    return $"{expr} = {result}";
}
