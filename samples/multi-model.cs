// =============================================================================
// multi-model.cs — Compare responses from different Copilot models
// Run: dotnet run samples/multi-model.cs
// =============================================================================

#:package GitHub.Copilot.SDK@0.1.23

using System.Diagnostics;
using GitHub.Copilot.SDK;

Console.WriteLine("🔀 Multi-Model Comparison");
Console.WriteLine("=========================");
Console.WriteLine("Compare how different models respond to the same prompt.");
Console.WriteLine();

await using var client = new CopilotClient();
await client.StartAsync();

// Models to compare — the Copilot SDK supports all models available via Copilot CLI
var models = new[] { "gpt-4o", "gpt-4.1" };

var prompt = args.Length > 0
    ? string.Join(" ", args)
    : "In exactly 2 sentences, explain what makes C# a great language for AI development.";

Console.WriteLine($"📤 Prompt: {prompt}");
Console.WriteLine();

foreach (var model in models)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"━━━ Model: {model} ━━━");
    Console.ResetColor();

    try
    {
        var sw = Stopwatch.StartNew();

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model
        });

        var done = new TaskCompletionSource();
        var response = "";

        session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    response = msg.Data.Content ?? "";
                    break;
                case SessionIdleEvent:
                    done.SetResult();
                    break;
                case SessionErrorEvent err:
                    response = $"❌ Error: {err.Data.Message}";
                    done.SetResult();
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await done.Task;

        sw.Stop();

        Console.WriteLine(response);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"⏱️  Response time: {sw.Elapsed.TotalSeconds:F1}s");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Failed: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
}

Console.WriteLine("✅ Comparison complete.");
