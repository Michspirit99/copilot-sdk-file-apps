#!/usr/bin/env dotnet
#:package GitHub.Copilot.SDK@*-*
#:package Microsoft.Extensions.AI@*-*
#:package Microsoft.Playwright@*-*

using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Playwright;
using System.ComponentModel;

// AI-Powered Browser Automation with Playwright
// This demonstrates using Copilot to control a browser, extract data, and perform testing

// Check for URL argument
if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run playwright-agent.cs -- <url> [task-description]");
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run playwright-agent.cs -- https://example.com");
    Console.WriteLine("  dotnet run playwright-agent.cs -- https://github.com \"Find the trending repositories\"");
    return 1;
}

string targetUrl = args[0];
string task = args.Length > 1 ? string.Join(" ", args[1..]) : "Describe what you see on the page";

Console.WriteLine($"🌐 Playwright AI Agent");
Console.WriteLine($"📍 Target: {targetUrl}");
Console.WriteLine($"🎯 Task: {task}\n");

// Initialize Playwright
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true // Set to false to watch the browser
});

var page = await browser.NewPageAsync();

// Define custom tools for browser automation
var navigateTool = AIFunctionFactory.Create(
    async ([Description("URL to navigate to")] string url) =>
    {
        await page.GotoAsync(url);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return new { success = true, url = page.Url, title = await page.TitleAsync() };
    },
    "navigate",
    "Navigate to a URL"
);

var getPageContentTool = AIFunctionFactory.Create(
    async () =>
    {
        var title = await page.TitleAsync();
        var textContent = await page.EvaluateAsync<string>("() => document.body.innerText");
        // Truncate if too long
        if (textContent.Length > 2000)
            textContent = textContent.Substring(0, 2000) + "... (truncated)";
        
        return new { title, url = page.Url, content = textContent };
    },
    "get_page_content",
    "Get the text content of the current page"
);

var clickElementTool = AIFunctionFactory.Create(
    async ([Description("CSS selector or text to click")] string selector) =>
    {
        try
        {
            // Try as selector first, then as text
            if (selector.StartsWith("#") || selector.StartsWith(".") || selector.Contains(">"))
            {
                await page.ClickAsync(selector);
            }
            else
            {
                await page.GetByText(selector).ClickAsync();
            }
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new { success = true, message = "Clicked successfully" };
        }
        catch (Exception ex)
        {
            return new { success = false, message = ex.Message };
        }
    },
    "click_element",
    "Click an element on the page"
);

var fillFormTool = AIFunctionFactory.Create(
    async (
        [Description("CSS selector of the input field")] string selector,
        [Description("Value to type")] string value) =>
    {
        try
        {
            await page.FillAsync(selector, value);
            return new { success = true, message = $"Filled '{selector}' with '{value}'" };
        }
        catch (Exception ex)
        {
            return new { success = false, message = ex.Message };
        }
    },
    "fill_form",
    "Fill a form field with a value"
);

var screenshotTool = AIFunctionFactory.Create(
    async ([Description("Filename for screenshot")] string filename) =>
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), filename);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
        return new { success = true, path };
    },
    "screenshot",
    "Take a screenshot of the current page"
);

// Create Copilot client with browser automation tools
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true,
    Tools = new[] { navigateTool, getPageContentTool, clickElementTool, fillFormTool, screenshotTool }
});

var responseComplete = new TaskCompletionSource();
var fullResponse = "";

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            fullResponse += delta.Data.DeltaContent;
            break;
            
        case ToolExecutionStartEvent toolStart:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n🔧 Using tool: {toolStart.Data.ToolName}");
            Console.ResetColor();
            break;
            
        case ToolExecutionCompleteEvent toolComplete:
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Tool completed");
            Console.ResetColor();
            break;
            
        case SessionIdleEvent:
            Console.WriteLine("\n");
            responseComplete.SetResult();
            break;
            
        case SessionErrorEvent error:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Error: {error.Data.Message}");
            Console.ResetColor();
            responseComplete.SetException(new Exception(error.Data.Message ?? "Unknown error"));
            break;
    }
});

// Send the automation task
var prompt = $@"You are a browser automation assistant. Use the provided tools to complete this task:

Target URL: {targetUrl}
Task: {task}

First, navigate to the URL, then analyze the page content and complete the requested task.
Use the tools in sequence as needed. Be specific about what you find.";

await session.SendAsync(new MessageOptions { Prompt = prompt });
await responseComplete.Task;

// Cleanup
await browser.CloseAsync();
playwright.Dispose();

Console.WriteLine($"\n✅ Automation complete!");
return 0;
