# Copilot SDK File-Based Apps 🚀

> **Zero-project AI apps in C#** — Combine .NET 10's file-based apps (`dotnet run app.cs`) with the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) to build AI-powered applications in a single `.cs` file. No `.csproj` needed.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Copilot SDK](https://img.shields.io/badge/Copilot_SDK-Technical_Preview-green.svg)](https://github.com/github/copilot-sdk)

## What Is This?

This repository demonstrates two cutting-edge .NET features working together:

1. **[.NET 10 File-Based Apps](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)** — Run C# files directly with `dotnet run app.cs`. No project file, no scaffolding, just code.
2. **[GitHub Copilot SDK](https://github.com/github/copilot-sdk)** — Programmatic access to GitHub Copilot's agent runtime. The same engine behind Copilot CLI, available as a NuGet package.

The result: **single-file AI applications** you can run instantly.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- [GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli) installed and available in PATH
- A [GitHub Copilot subscription](https://github.com/features/copilot#pricing) (free tier available)
- Authenticated with Copilot CLI (`copilot auth login`)

## Quick Start

```bash
# Clone this repo
git clone https://github.com/YOUR_USERNAME/copilot-sdk-file-apps.git
cd copilot-sdk-file-apps

# Run any example — no build step required!
dotnet run samples/hello-copilot.cs
```

That's it. No `dotnet new`, no `.csproj`, no `dotnet restore`. Just run.

## Samples

| File | Description |
|------|-------------|
| [`samples/hello-copilot.cs`](samples/hello-copilot.cs) | Minimal "Hello World" — send a prompt, get a response |
| [`samples/streaming-chat.cs`](samples/streaming-chat.cs) | Stream responses token-by-token in real time |
| [`samples/interactive-chat.cs`](samples/interactive-chat.cs) | Full interactive chat loop in the terminal |
| [`samples/code-reviewer.cs`](samples/code-reviewer.cs) | AI-powered code review — pass any file for analysis |
| [`samples/custom-tools.cs`](samples/custom-tools.cs) | Define custom tools the AI agent can invoke |
| [`samples/multi-model.cs`](samples/multi-model.cs) | Compare responses across different models |
| [`samples/file-summarizer.cs`](samples/file-summarizer.cs) | Summarize any text file using AI |
| [`samples/git-commit-writer.cs`](samples/git-commit-writer.cs) | Generate commit messages from staged changes |

### Run any sample

```bash
# Basic usage
dotnet run samples/hello-copilot.cs

# Pass arguments to samples that accept them
dotnet run samples/code-reviewer.cs -- path/to/file.cs
dotnet run samples/file-summarizer.cs -- README.md
```

## How It Works

### File-Based Apps (`.NET 10`)

.NET 10 introduced `dotnet run app.cs` — the ability to run a single `.cs` file without a project. File-level directives replace what `.csproj` files traditionally did:

```csharp
#:package GitHub.Copilot.SDK       // NuGet package reference
#:package Microsoft.Extensions.AI  // Add as many as you need

// Your code starts here — top-level statements, no boilerplate
Console.WriteLine("Hello from a file-based app!");
```

Key directives:
- `#:package PackageName@Version` — Add a NuGet package reference
- `#:sdk Microsoft.NET.Sdk.Web` — Change the SDK (for web apps)
- `#:property Key=Value` — Set MSBuild properties

### GitHub Copilot SDK

The Copilot SDK (`GitHub.Copilot.SDK`) gives you programmatic access to the Copilot agent runtime:

```csharp
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o"
});

session.On(evt =>
{
    if (evt is AssistantMessageEvent msg)
        Console.WriteLine(msg.Data.Content);
});

await session.SendAsync(new MessageOptions { Prompt = "Explain async/await" });
```

## Converting to a Full Project

When a file-based app outgrows its single file, convert it:

```bash
dotnet project convert samples/hello-copilot.cs
```

This generates a directory with a proper `.csproj`, preserving all your `#:` directives as MSBuild properties and package references.

## BYOK (Bring Your Own Key)

Don't have a Copilot subscription? Use your own API keys:

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    }
});
```

See [`samples/custom-tools.cs`](samples/custom-tools.cs) for a complete BYOK example pattern.

## Project Structure

```
copilot-sdk-file-apps/
├── samples/                    # All runnable examples
│   ├── hello-copilot.cs        # Minimal example
│   ├── streaming-chat.cs       # Streaming responses
│   ├── interactive-chat.cs     # Interactive terminal chat
│   ├── code-reviewer.cs        # AI code review
│   ├── custom-tools.cs         # Custom tool definitions
│   ├── multi-model.cs          # Multi-model comparison
│   ├── file-summarizer.cs      # File summarization
│   └── git-commit-writer.cs    # Git commit message generation
├── README.md
├── LICENSE
├── .gitignore
└── .github/
    └── FUNDING.yml
```

## Why File-Based Apps + Copilot SDK?

| Traditional Approach | File-Based Approach |
|---|---|
| `dotnet new console` | Just create a `.cs` file |
| Edit `.csproj` for packages | `#:package` directive inline |
| `dotnet restore && dotnet run` | `dotnet run app.cs` |
| Multiple files for simple tasks | Single file, top-level statements |
| Project scaffolding overhead | Zero ceremony |

File-based apps make C# as approachable as Python for quick AI experiments while retaining the full power of the .NET ecosystem.

## Contributing

Contributions welcome! Feel free to:
- Add new sample files demonstrating Copilot SDK features
- Improve existing samples
- Fix bugs or improve documentation

## License

[MIT](LICENSE)

## Resources

- [.NET 10 File-Based Apps Announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
- [GitHub Copilot SDK .NET README](https://github.com/github/copilot-sdk/tree/main/dotnet)
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [.NET 10 SDK Docs](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/sdk)
