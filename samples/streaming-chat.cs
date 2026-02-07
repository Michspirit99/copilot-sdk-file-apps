// =============================================================================
// streaming-chat.cs — Stream Copilot responses token-by-token
// Run: dotnet run samples/streaming-chat.cs
// =============================================================================

#:package GitHub.Copilot.SDK@*-*

using GitHub.Copilot.SDK;

Console.WriteLine("🌊 Streaming Chat — Watch responses appear in real time");
Console.WriteLine("=======================================================");
Console.WriteLine();

await using var client = new CopilotClient();
await client.StartAsync();

// Enable streaming in session config
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true
});

var done = new TaskCompletionSource();

Console.Write("💬 ");

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            // Print each token as it arrives — no newline
            Console.Write(delta.Data.DeltaContent);
            break;
        case AssistantMessageEvent:
            // Final message received — we already printed via deltas
            Console.WriteLine();
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

var prompt = args.Length > 0
    ? string.Join(" ", args)
    : "Write a short poem about C# and AI working together. Be creative!";

Console.WriteLine($"📤 Prompt: {prompt}");
Console.WriteLine();
Console.Write("💬 ");

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine("✅ Streaming complete.");
