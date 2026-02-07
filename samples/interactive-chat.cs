// =============================================================================
// interactive-chat.cs — Full interactive terminal chat with Copilot
// Run: dotnet run samples/interactive-chat.cs
// =============================================================================

#:package GitHub.Copilot.SDK@0.1.23

using GitHub.Copilot.SDK;

Console.WriteLine("💬 Interactive Copilot Chat");
Console.WriteLine("==========================");
Console.WriteLine("Type your messages below. Type 'exit' or 'quit' to end.");
Console.WriteLine("Type 'clear' to start a new session.");
Console.WriteLine();

await using var client = new CopilotClient();
await client.StartAsync();

var model = "gpt-4o";
CopilotSession session = await client.CreateSessionAsync(new SessionConfig
{
    Model = model,
    Streaming = true
});

Console.WriteLine($"✅ Connected (model: {model})");
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You > ");
    Console.ResetColor();

    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("👋 Goodbye!");
        break;
    }

    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
    {
        await session.DisposeAsync();
        session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true
        });
        Console.WriteLine("🔄 Session cleared. Starting fresh.");
        Console.WriteLine();
        continue;
    }

    var done = new TaskCompletionSource();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("AI  > ");
    Console.ResetColor();

    using var subscription = session.On(evt =>
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta:
                Console.Write(delta.Data.DeltaContent);
                break;
            case AssistantMessageEvent:
                Console.WriteLine();
                Console.WriteLine();
                break;
            case SessionIdleEvent:
                done.TrySetResult();
                break;
            case SessionErrorEvent err:
                Console.WriteLine($"\n❌ Error: {err.Data.Message}");
                Console.WriteLine();
                done.TrySetResult();
                break;
        }
    });

    await session.SendAsync(new MessageOptions { Prompt = input });
    await done.Task;
}

await session.DisposeAsync();
