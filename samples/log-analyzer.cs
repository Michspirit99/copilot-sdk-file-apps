#!/usr/bin/env dotnet
#:package GitHub.Copilot.SDK@0.1.23
#:package Microsoft.Extensions.AI@10.2.0

using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;

// AI-Powered Log File Analyzer
// Analyzes application logs to find errors, patterns, performance issues, and security concerns

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run log-analyzer.cs -- <log-file-path> [analysis-type]");
    Console.WriteLine("\nAnalysis types:");
    Console.WriteLine("  errors     - Find and categorize all errors");
    Console.WriteLine("  security   - Identify potential security issues");
    Console.WriteLine("  performance- Find performance bottlenecks");
    Console.WriteLine("  summary    - General overview (default)");
    Console.WriteLine("\nExample:");
    Console.WriteLine("  dotnet run log-analyzer.cs -- app.log errors");
    return 1;
}

string logFilePath = args[0];
string analysisType = args.Length > 1 ? args[1].ToLower() : "summary";

if (!File.Exists(logFilePath))
{
    Console.WriteLine($"❌ Error: File not found: {logFilePath}");
    return 1;
}

Console.WriteLine($"📊 AI Log Analyzer");
Console.WriteLine($"📁 File: {logFilePath}");
Console.WriteLine($"🔍 Analysis: {analysisType}\n");

// Read log file
var logContent = await File.ReadAllTextAsync(logFilePath);
var fileInfo = new FileInfo(logFilePath);
var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

Console.WriteLine($"📏 File size: {fileSizeMB:F2} MB");
Console.WriteLine($"📝 Lines: {logContent.Split('\n').Length:N0}\n");

// Define analysis tools
var extractErrorsTool = AIFunctionFactory.Create(
    ([Description("The log content to analyze")] string content) =>
    {
        var errors = new List<string>();
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(line);
                if (errors.Count >= 50) break; // Limit to first 50 errors
            }
        }

        var top = errors.Take(20).Select(l => l.Trim()).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"errorCount: {errors.Count}");
        sb.AppendLine("errors:");
        if (top.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var line in top)
                sb.AppendLine($"- {line}");
        }
        return sb.ToString();
    },
    "extract_errors",
    "Extract error and exception lines from logs"
);

var countPatternTool = AIFunctionFactory.Create(
    (
        [Description("The log content")] string content,
        [Description("Pattern to search for (case-insensitive)")] string pattern) =>
    {
        var count = 0;
        var examples = new List<string>();
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                count++;
                if (examples.Count < 5)
                    examples.Add(line.Trim());
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"pattern: {pattern}");
        sb.AppendLine($"count: {count}");
        sb.AppendLine("examples:");
        if (examples.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var ex in examples)
                sb.AppendLine($"- {ex}");
        }
        return sb.ToString();
    },
    "count_pattern",
    "Count occurrences of a pattern in logs"
);

var getTimeRangeTool = AIFunctionFactory.Create(
    ([Description("The log content")] string content) =>
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0 ? lines[0] : "";
        var lastLine = lines.Length > 0 ? lines[^1] : "";

        var firstEntry = firstLine.Length > 200 ? firstLine.Substring(0, 200) : firstLine;
        var lastEntry = lastLine.Length > 200 ? lastLine.Substring(0, 200) : lastLine;
        return $"totalLines: {lines.Length}\nfirstEntry: {firstEntry}\nlastEntry: {lastEntry}\n";
    },
    "get_time_range",
    "Get the time range covered by the logs"
);

var findSlowOperationsTool = AIFunctionFactory.Create(
    ([Description("The log content")] string content) =>
    {
        var slowOps = new List<string>();
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            // Look for timing patterns like "took 1234ms" or "Duration: 5000ms"
            if ((line.Contains("ms", StringComparison.OrdinalIgnoreCase) || 
                 line.Contains("seconds", StringComparison.OrdinalIgnoreCase)) &&
                (line.Contains("slow", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                 System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{4,}ms")))
            {
                slowOps.Add(line.Trim());
                if (slowOps.Count >= 20) break;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("slowOperations:");
        if (slowOps.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var op in slowOps)
                sb.AppendLine($"- {op}");
        }
        return sb.ToString();
    },
    "find_slow_operations",
    "Find operations that took a long time"
);

// Truncate log content if too large (only send sample to AI)
string logSample = logContent;
if (logContent.Length > 50000)
{
    var lines = logContent.Split('\n');
    var sampleSize = 200;
    var start = lines.Take(sampleSize);
    var middle = lines.Skip(lines.Length / 2).Take(sampleSize);
    var end = lines.TakeLast(sampleSize);
    
    logSample = string.Join('\n', start) + 
                "\n\n... (middle section omitted) ...\n\n" +
                string.Join('\n', middle) +
                "\n\n... (more content omitted) ...\n\n" +
                string.Join('\n', end);
}

// Create Copilot session
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true,
    Tools = new[] { extractErrorsTool, countPatternTool, getTimeRangeTool, findSlowOperationsTool }
});

var analysisComplete = new TaskCompletionSource();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            break;
            
        case ToolExecutionStartEvent toolStart:
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🔍 Analyzing: {toolStart.Data.ToolName}...");
            Console.ResetColor();
            break;
            
        case SessionIdleEvent:
            Console.WriteLine("\n");
            analysisComplete.SetResult();
            break;
    }
});

// Create analysis prompt based on type
var prompt = analysisType switch
{
    "errors" => $@"Analyze this log file and provide a detailed error analysis:
1. Extract all errors/exceptions using the extract_errors tool
2. Categorize the errors by type
3. Identify the most frequent errors
4. Suggest potential root causes
5. Recommend fixes

Log file (sample):
{logSample}",

    "security" => $@"Perform a security-focused analysis of this log file:
1. Look for failed authentication attempts (use count_pattern)
2. Identify suspicious patterns or potential attacks
3. Check for sensitive data exposure
4. Look for unusual access patterns
5. Provide security recommendations

Log file (sample):
{logSample}",

    "performance" => $@"Analyze this log file for performance issues:
1. Find slow operations using find_slow_operations
2. Identify performance bottlenecks
3. Look for timeout patterns
4. Check for resource exhaustion indicators
5. Provide performance optimization suggestions

Log file (sample):
{logSample}",

    _ => $@"Provide a comprehensive analysis of this log file:
1. Use get_time_range to understand the coverage
2. Extract and categorize errors
3. Identify key patterns and trends
4. Highlight any concerning issues
5. Provide actionable recommendations

Log file (sample):
{logSample}"
};

await session.SendAsync(new MessageOptions { Prompt = prompt });
await analysisComplete.Task;

Console.WriteLine($"✅ Analysis complete!");
Console.WriteLine($"💡 Tip: Try different analysis types: errors, security, performance");

return 0;
