# Contributing to Copilot SDK File-Based Apps

Thanks for your interest in contributing! This project welcomes contributions of all kinds.

## Adding a New Sample

1. Create a new `.cs` file in the `samples/` directory
2. Start with the `#:package GitHub.Copilot.SDK` directive
3. Add a header comment block with the filename, description, and run command
4. Follow the patterns established in existing samples
5. Update `README.md` to include your new sample in the table

### Sample Template

```csharp
// =============================================================================
// your-sample.cs — Short description
// Run: dotnet run samples/your-sample.cs
// =============================================================================

#:package GitHub.Copilot.SDK@*-*

using GitHub.Copilot.SDK;

Console.WriteLine("Your sample name");
Console.WriteLine("=================");

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o"
});

// Your sample code here...
```

### Guidelines

- Each sample should be **self-contained** in a single `.cs` file
- Use `#:package` directives — never add a `.csproj`
- Include clear console output so users understand what's happening
- Handle errors gracefully
- Support command-line arguments via `args` where appropriate
- Keep samples focused — demonstrate one concept per file

## Reporting Issues

- Use GitHub Issues to report bugs
- Include the .NET SDK version (`dotnet --version`)
- Include the Copilot CLI version (`copilot --version`)
- Paste any error output

## Code Style

- Use top-level statements (no `Main` method)
- Use `var` where the type is obvious
- Use C# 14 / latest language features
- Use raw string literals for multi-line strings
- Prefer pattern matching for event handling

## Testing

Since these are file-based apps, test by running them:

```bash
dotnet run samples/your-sample.cs
```

Make sure the sample:
- Compiles without errors
- Runs successfully with a Copilot CLI connection
- Handles the "no arguments" case gracefully (if applicable)
- Fails gracefully if Copilot CLI is not available
