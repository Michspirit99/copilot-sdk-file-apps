#!/usr/bin/env dotnet
#:package GitHub.Copilot.SDK@*-*
#:package Microsoft.Extensions.AI@*-*

using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

// AI-Powered Test Data Generator
// Generates realistic test data for databases, APIs, forms, etc.

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run test-data-generator.cs -- <schema> <count> [format]");
    Console.WriteLine("\nFormats:");
    Console.WriteLine("  json     - JSON array (default)");
    Console.WriteLine("  sql      - SQL INSERT statements");
    Console.WriteLine("  csv      - CSV file");
    Console.WriteLine("  csharp   - C# class instances");
    Console.WriteLine("\nSchema can be:");
    Console.WriteLine("  - Schema name: 'user', 'product', 'order', 'customer'");
    Console.WriteLine("  - Custom: '{name:string, age:int, email:string}'");
    Console.WriteLine("\nExamples:");
    Console.WriteLine("  dotnet run test-data-generator.cs -- user 10");
    Console.WriteLine("  dotnet run test-data-generator.cs -- product 50 sql");
    Console.WriteLine("  dotnet run test-data-generator.cs -- \"{name:string,email:string}\" 20 json");
    return 1;
}

string schema = args[0];
if (!int.TryParse(args[1], out int count) || count <= 0 || count > 1000)
{
    Console.WriteLine("❌ Error: Count must be between 1 and 1000");
    return 1;
}

string format = args.Length > 2 ? args[2].ToLower() : "json";

Console.WriteLine($"🎲 Test Data Generator");
Console.WriteLine($"📋 Schema: {schema}");
Console.WriteLine($"🔢 Count: {count}");
Console.WriteLine($"📝 Format: {format}\n");

// Define data generation tools
var generateRecordTool = AIFunctionFactory.Create(
    (
        [Description("Field name")] string fieldName,
        [Description("Data type (string, int, email, phone, date, etc.)")] string dataType,
        [Description("Index for variety")] int index) =>
    {
        // This tool helps AI understand what kind of data to generate
        // The actual generation is done by the AI model
        return new { field = fieldName, type = dataType, index };
    },
    "generate_record",
    "Generate a single data record field"
);

var validateDataTool = AIFunctionFactory.Create(
    ([Description("JSON data to validate")] string jsonData) =>
    {
        try
        {
            var doc = JsonDocument.Parse(jsonData);
            var recordCount = doc.RootElement.GetArrayLength();
            return new { valid = true, recordCount };
        }
        catch (Exception ex)
        {
            return new { valid = false, error = ex.Message };
        }
    },
    "validate_data",
    "Validate generated test data"
);

// Create Copilot session
await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4o",
    Streaming = true,
    Tools = new[] { generateRecordTool, validateDataTool }
});

var generationComplete = new TaskCompletionSource();
var outputBuilder = new System.Text.StringBuilder();
var jsonOutput = "";

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            var content = delta.Data.DeltaContent ?? "";
            Console.Write(content);
            outputBuilder.Append(content);
            break;
            
        case AssistantMessageEvent msg:
            jsonOutput = msg.Data.Content ?? "";
            break;
            
        case ToolExecutionStartEvent toolStart:
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(".");
            Console.ResetColor();
            break;
            
        case SessionIdleEvent:
            Console.WriteLine("\n");
            generationComplete.SetResult();
            break;
    }
});

// Define common schemas
var schemaDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["user"] = "{ id:int, firstName:string, lastName:string, email:string, username:string, age:int, createdAt:datetime }",
    ["product"] = "{ id:int, name:string, description:string, price:decimal, category:string, stock:int, sku:string }",
    ["order"] = "{ id:int, customerId:int, orderDate:datetime, totalAmount:decimal, status:string, shippingAddress:string }",
    ["customer"] = "{ id:int, companyName:string, contactName:string, email:string, phone:string, address:string, city:string, country:string }",
    ["employee"] = "{ id:int, firstName:string, lastName:string, email:string, department:string, position:string, salary:decimal, hireDate:datetime }",
    ["event"] = "{ id:int, name:string, description:string, startDate:datetime, endDate:datetime, location:string, capacity:int, ticketPrice:decimal }"
};

// Resolve schema
string resolvedSchema = schemaDefinitions.ContainsKey(schema) ? schemaDefinitions[schema] : schema;

// Generate prompt based on format
var constraints = @"
IMPORTANT DATA QUALITY RULES:
- Names should be realistic and diverse (different ethnicities, cultures)
- Emails must follow valid format and match names when appropriate
- Dates should be realistic and logical (hire dates before current date, etc.)
- Prices should have realistic ranges for the category
- Phone numbers should follow valid formats
- Addresses should be complete and realistic
- IDs should be sequential starting from 1
- Categories/statuses should be consistent and predefined set
- No placeholder or dummy data like 'test@test.com' or 'John Doe'";

var prompt = format switch
{
    "sql" => $@"Generate {count} realistic test data records for SQL INSERT statements.

Schema: {resolvedSchema}

Requirements:
1. Generate {count} records with realistic, diverse data
2. Format as SQL INSERT statements for a table with appropriate columns
3. Include the CREATE TABLE statement first
4. Use proper SQL data types
5. Ensure referential integrity if there are foreign keys
{constraints}

Generate complete SQL statements ready to execute.",

    "csv" => $@"Generate {count} realistic test data records in CSV format.

Schema: {resolvedSchema}

Requirements:
1. First line must be the CSV header with column names
2. Generate {count} data rows with realistic, diverse data
3. Properly escape commas and quotes in values
4. Use consistent date format (YYYY-MM-DD)
{constraints}

Output only the CSV data, no code blocks or markdown.",

    "csharp" => $@"Generate {count} realistic test data records as C# code.

Schema: {resolvedSchema}

Requirements:
1. Define a C# record or class matching the schema
2. Create a method that returns List<YourClass> with {count} items
3. Use realistic, diverse test data
4. Include proper using statements
5. Make it compilable as-is
{constraints}

Generate complete C# code.",

    _ => $@"Generate {count} realistic test data records in JSON format.

Schema: {resolvedSchema}

Requirements:
1. Generate a JSON array with {count} objects
2. Each object should match the schema exactly
3. Use realistic, diverse data - no placeholders
4. Ensure data consistency (e.g., emails match names)
5. Output ONLY the JSON array, no markdown or code blocks
{constraints}

Output must be valid, parseable JSON."
};

Console.WriteLine("🔄 Generating test data...\n");

await session.SendAsync(new MessageOptions { Prompt = prompt });
await generationComplete.Task;

// Extract JSON from output if needed (handle markdown code blocks)
var output = outputBuilder.ToString();
if (format == "json")
{
    // Try to extract JSON from markdown code blocks
    var jsonStart = output.IndexOf('[');
    var jsonEnd = output.LastIndexOf(']');
    if (jsonStart >= 0 && jsonEnd > jsonStart)
    {
        output = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
    }
}

// Save to file
var extension = format switch
{
    "sql" => ".sql",
    "csv" => ".csv",
    "csharp" => ".cs",
    _ => ".json"
};

var fileName = $"test-data-{schema}{extension}";
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

await File.WriteAllTextAsync(outputPath, output);

Console.WriteLine($"✅ Generated {count} records!");
Console.WriteLine($"💾 Saved to: {outputPath}");

// Show preview
Console.WriteLine($"\n📄 Preview (first 500 chars):");
Console.WriteLine(output.Length > 500 ? output.Substring(0, 500) + "..." : output);

Console.WriteLine($"\n💡 Tip: Use different schemas: user, product, order, customer, employee");

return 0;
