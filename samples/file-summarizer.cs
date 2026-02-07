// =============================================================================
// file-summarizer.cs — Summarize any text file using Copilot
// Run: dotnet run samples/file-summarizer.cs -- path/to/file.txt
// =============================================================================

#:package GitHub.Copilot.SDK

using GitHub.Copilot.SDK;

if (args.Length == 0)
{
    Console.WriteLine("📄 File Summarizer — AI-powered document summarization");
    Console.WriteLine("======================================================");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run samples/file-summarizer.cs -- <file-path> [--bullets]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --bullets    Output as bullet points instead of prose");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run samples/file-summarizer.cs -- README.md");
    Console.WriteLine("  dotnet run samples/file-summarizer.cs -- docs/spec.txt --bullets");
    return;
}

var filePath = args[0];
var useBullets = args.Any(a => a == "--bullets");

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"❌ File not found: {filePath}");
    return;
}

var fileContent = await File.ReadAllTextAsync(filePath);
var fileName = Path.GetFileName(filePath);
var lineCount = fileContent.Split('\n').Length;
var charCount = fileContent.Length;

Console.WriteLine("📄 File Summarizer");
Console.WriteLine("==================");
Console.WriteLine($"📁 File: {filePath}");
Console.WriteLine($"📏 Size: {lineCount} lines, {charCount:N0} characters");
Console.WriteLine();

// Truncate very large files to avoid exceeding context limits
const int maxChars = 50_000;
if (fileContent.Length > maxChars)
{
    fileContent = fileContent[..maxChars];
    Console.WriteLine($"⚠️  File truncated to first {maxChars:N0} characters for summarization.");
    Console.WriteLine();
}

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true
});

var done = new TaskCompletionSource();

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

var format = useBullets
    ? "Use bullet points for each key point."
    : "Write a concise prose summary.";

var prompt = $"""
    Summarize the following file ({fileName}).
    {format}
    Focus on the most important information and key takeaways.
    Keep the summary under 300 words.

    ---
    {fileContent}
    ---
    """;

Console.WriteLine("📝 Summary:");
Console.WriteLine();

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine();
Console.WriteLine("✅ Summarization complete.");
