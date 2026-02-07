// =============================================================================
// git-commit-writer.cs — Generate commit messages from staged git changes
// Run: dotnet run samples/git-commit-writer.cs
// =============================================================================

#:package GitHub.Copilot.SDK@0.1.23

using System.Diagnostics;
using GitHub.Copilot.SDK;

Console.WriteLine("✍️  Git Commit Writer — AI-generated commit messages");
Console.WriteLine("====================================================");
Console.WriteLine();

// Get the staged diff from git
var diff = await RunGitCommandAsync("diff --cached");

if (string.IsNullOrWhiteSpace(diff))
{
    // If nothing staged, show the unstaged diff as a preview
    diff = await RunGitCommandAsync("diff");

    if (string.IsNullOrWhiteSpace(diff))
    {
        Console.WriteLine("ℹ️  No changes detected (staged or unstaged).");
        Console.WriteLine("   Stage some changes with 'git add' and run again.");
        return;
    }

    Console.WriteLine("⚠️  No staged changes found. Showing unstaged changes preview.");
    Console.WriteLine("   Stage changes with 'git add' before committing.");
    Console.WriteLine();
}
else
{
    Console.WriteLine("📋 Found staged changes.");
    Console.WriteLine();
}

// Also get the list of changed files for context
var stagedFiles = await RunGitCommandAsync("diff --cached --name-status");
if (!string.IsNullOrWhiteSpace(stagedFiles))
{
    Console.WriteLine("📁 Changed files:");
    foreach (var line in stagedFiles.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        Console.WriteLine($"   {line}");
    }
    Console.WriteLine();
}

// Truncate large diffs
const int maxDiffChars = 30_000;
if (diff.Length > maxDiffChars)
{
    diff = diff[..maxDiffChars];
    Console.WriteLine($"⚠️  Diff truncated to {maxDiffChars:N0} characters.");
    Console.WriteLine();
}

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
            You are an expert at writing git commit messages.
            Follow the Conventional Commits specification.
            Format: <type>(<scope>): <description>
            
            Types: feat, fix, docs, style, refactor, perf, test, build, ci, chore
            
            Rules:
            - Subject line max 72 characters
            - Use imperative mood ("add" not "added")
            - Don't end subject with period
            - Separate subject from body with blank line
            - Body should explain what and why, not how
            
            Provide exactly 3 options ranked from best to least, numbered 1-3.
            """
    }
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

var prompt = $"""
    Generate commit messages for these changes:

    Files changed:
    {stagedFiles}

    Diff:
    ```
    {diff}
    ```
    """;

Console.WriteLine("💬 Suggested commit messages:");
Console.WriteLine();

await session.SendAsync(new MessageOptions { Prompt = prompt });
await done.Task;

Console.WriteLine("✅ Done! Copy your preferred message and use: git commit -m \"<message>\"");

// Helper to run a git command and capture output
static async Task<string> RunGitCommandAsync(string arguments)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return "";

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
    catch
    {
        return "";
    }
}
