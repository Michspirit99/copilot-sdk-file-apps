// =============================================================================
// hello-copilot.cs — Minimal Copilot SDK example as a file-based app
// Run: dotnet run samples/hello-copilot.cs
// =============================================================================

#:package GitHub.Copilot.SDK@*-*

using GitHub.Copilot.SDK;

Console.WriteLine("🤖 Hello Copilot SDK — File-Based App Demo");
Console.WriteLine("==========================================");
Console.WriteLine();

// Create and start the Copilot client
await using var client = new CopilotClient();
await client.StartAsync();

Console.WriteLine("✅ Connected to Copilot CLI server");

// Create a session with a model
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o"
});

Console.WriteLine("✅ Session created");
Console.WriteLine();

// Use TaskCompletionSource to wait for the response
var done = new TaskCompletionSource();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageEvent msg:
            Console.WriteLine("💬 Copilot says:");
            Console.WriteLine(msg.Data.Content);
            Console.WriteLine();
            break;
        case SessionIdleEvent:
            done.SetResult();
            break;
        case SessionErrorEvent err:
            Console.WriteLine($"❌ Error: {err.Data.Message}");
            done.SetResult();
            break;
    }
});

// Send a simple prompt
var prompt = "What are 3 cool things about C# file-based apps in .NET 10? Keep it brief.";
Console.WriteLine($"📤 Sending: {prompt}");
Console.WriteLine();

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine("✅ Done! This entire app is a single .cs file — no project needed.");
