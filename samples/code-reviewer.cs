// =============================================================================
// code-reviewer.cs — AI-powered code review using Copilot SDK
// Run: dotnet run samples/code-reviewer.cs -- path/to/file.cs
// =============================================================================

#:package GitHub.Copilot.SDK@*-*

using GitHub.Copilot.SDK;

if (args.Length == 0)
{
    Console.WriteLine("📋 Code Reviewer — AI-powered code analysis");
    Console.WriteLine("============================================");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run samples/code-reviewer.cs -- <file-path>");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run samples/code-reviewer.cs -- Program.cs");
    Console.WriteLine("  dotnet run samples/code-reviewer.cs -- src/MyService.cs");
    return;
}

var filePath = args[0];

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"❌ File not found: {filePath}");
    return;
}

var fileContent = await File.ReadAllTextAsync(filePath);
var fileName = Path.GetFileName(filePath);
var extension = Path.GetExtension(filePath).TrimStart('.');

Console.WriteLine("📋 Code Reviewer — AI-powered code analysis");
Console.WriteLine("============================================");
Console.WriteLine($"📄 Reviewing: {filePath} ({fileContent.Split('\n').Length} lines)");
Console.WriteLine();

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true,
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Append,
        Content = """
            You are a senior code reviewer. When reviewing code:
            1. Identify bugs, potential issues, and security concerns
            2. Suggest performance improvements
            3. Comment on code style and readability
            4. Highlight what's done well
            5. Keep feedback constructive and actionable
            Format your review with clear sections and use markdown.
            """
    }
});

var done = new TaskCompletionSource();

Console.Write("💬 ");

session.On(evt =>
{
    switch (evt)
    {
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

var prompt = $"""
    Please review the following {extension} file ({fileName}):

    ```{extension}
    {fileContent}
    ```
    """;

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine();
Console.WriteLine("✅ Review complete.");
